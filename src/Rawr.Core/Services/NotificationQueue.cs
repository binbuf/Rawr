using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Core.Services;

public class NotificationQueue : IDisposable
{
    private readonly IAlertScheduler _scheduler;
    private readonly ILogger<NotificationQueue> _logger;
    
    // Thread-safe collection for alerts
    private readonly List<CalendarEvent> _activeAlerts = new();
    private readonly object _lock = new();

    public event EventHandler<CalendarEvent>? AlertAdded;
    public event EventHandler<CalendarEvent>? AlertRemoved;

    public NotificationQueue(IAlertScheduler scheduler, ILogger<NotificationQueue> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
        _scheduler.AlertTriggered += OnAlertTriggered;
    }

    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromSeconds(60);

    private void OnAlertTriggered(object? sender, CalendarEvent evt)
    {
        _logger.LogInformation("NotificationQueue received alert: {Title}", evt.Title);
        lock (_lock)
        {
            if (_activeAlerts.Any(e => e.Uid == evt.Uid && e.Start == evt.Start))
                return;

            // Check for simultaneous events within the coalesce window
            var simultaneous = _activeAlerts.FirstOrDefault(e =>
                Math.Abs((e.Start - evt.Start).TotalSeconds) <= CoalesceWindow.TotalSeconds);

            if (simultaneous != null)
            {
                // Priority: Calendar > Interval. If same type, first arrival wins as primary.
                if (evt.EventType == EventType.Calendar && simultaneous.EventType == EventType.Interval)
                {
                    // New calendar event takes priority over existing interval
                    _activeAlerts.Remove(simultaneous);
                    evt.SimultaneousEvents.Add(simultaneous);
                    // Carry over any previously coalesced events
                    evt.SimultaneousEvents.AddRange(simultaneous.SimultaneousEvents);
                    simultaneous.SimultaneousEvents.Clear();
                    _activeAlerts.Add(evt);
                    AlertRemoved?.Invoke(this, simultaneous);
                    AlertAdded?.Invoke(this, evt);
                }
                else
                {
                    // Existing keeps priority; add new as simultaneous
                    simultaneous.SimultaneousEvents.Add(evt);
                    _logger.LogInformation("Coalesced {NewTitle} into {ExistingTitle}", evt.Title, simultaneous.Title);
                }
                return;
            }

            _activeAlerts.Add(evt);
            AlertAdded?.Invoke(this, evt);
        }
    }

    public void Dismiss(CalendarEvent evt)
    {
        lock (_lock)
        {
            var existing = _activeAlerts.FirstOrDefault(e => e.Uid == evt.Uid && e.Start == evt.Start);
            if (existing != null)
            {
                _activeAlerts.Remove(existing);
                AlertRemoved?.Invoke(this, existing);
            }
        }
    }

    public List<CalendarEvent> GetActiveAlerts()
    {
        lock (_lock)
        {
            return _activeAlerts.ToList();
        }
    }

    public void Dispose()
    {
        _scheduler.AlertTriggered -= OnAlertTriggered;
    }
}

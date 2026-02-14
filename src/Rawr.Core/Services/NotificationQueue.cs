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

    private void OnAlertTriggered(object? sender, CalendarEvent evt)
    {
        _logger.LogInformation("NotificationQueue received alert: {Title}", evt.Title);
        lock (_lock)
        {
            if (!_activeAlerts.Any(e => e.Uid == evt.Uid && e.Start == evt.Start))
            {
                _activeAlerts.Add(evt);
                AlertAdded?.Invoke(this, evt);
            }
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

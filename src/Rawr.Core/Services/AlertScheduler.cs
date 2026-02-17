using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Core.Services;

public class AlertScheduler : IAlertScheduler, IDisposable
{
    private readonly ICalendarRepository _repository;
    private readonly ISettingsManager _settingsManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AlertScheduler> _logger;
    
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _wakeCts;
    private Task? _schedulerTask;
    private readonly Dictionary<string, DateTimeOffset> _firedEvents = new(); // Key -> Event Start Time
    private readonly string _firedEventsPath;

    public event EventHandler<CalendarEvent>? AlertTriggered;

    public DateTimeOffset? SnoozeUntil { get; set; }
    public DateTimeOffset? NextAlertTime { get; private set; }
    public string? NextAlertDescription { get; private set; }

    public AlertScheduler(
        ICalendarRepository repository,
        ISettingsManager settingsManager,
        TimeProvider timeProvider,
        ILogger<AlertScheduler> logger)
    {
        _repository = repository;
        _settingsManager = settingsManager;
        _timeProvider = timeProvider;
        _logger = logger;
        _firedEventsPath = Path.Combine(settingsManager.AppDataPath, "fired_events.json");
        LoadFiredEvents();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_schedulerTask != null && !_schedulerTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _schedulerTask = RunSchedulerLoopAsync(_cts.Token);
        _logger.LogInformation("AlertScheduler started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_schedulerTask != null)
            {
                try
                {
                    await _schedulerTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            _cts.Dispose();
            _cts = null;
        }
        _logger.LogInformation("AlertScheduler stopped.");
    }

    private async Task RunSchedulerLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var now = _timeProvider.GetUtcNow();
                var settings = _settingsManager.Settings.General;
                var missedThreshold = TimeSpan.FromMinutes(settings.MissedEventThresholdMinutes);

                // Cleanup old fired events to prevent memory leak
                CleanupFiredEvents(now, missedThreshold);

                if (SnoozeUntil.HasValue && now < SnoozeUntil.Value)
                {
                    // Alerts are snoozed. We still want to calculate wait time to next check.
                    await WaitAsync(TimeSpan.FromSeconds(settings.HeartbeatIntervalSeconds), token);
                    continue;
                }

                var gracePeriod = TimeSpan.FromSeconds(settings.GracePeriodSeconds);
                var events = await _repository.GetAllEventsAsync(token);
                var calendarSettings = _settingsManager.Settings.Calendar;

                // 1. Process Due Events (Grace period or Catch-up)
                // Grace period: events missed by a small window (e.g. after sleep) fire normally
                // Catch-up: events between grace and missed threshold fire with indicator
                var dueEvents = events
                    .Where(e =>
                        e.Start <= now &&
                        e.Start >= now - missedThreshold &&
                        !IsFired(e))
                    .OrderBy(e => e.Start)
                    .ToList();

                foreach (var evt in dueEvents)
                {
                    var age = now - evt.Start;
                    if (age > gracePeriod)
                    {
                        // Catch-up alert — event is older than grace period
                        TriggerCatchUpAlert(evt);
                    }
                    else
                    {
                        TriggerAlert(evt);
                    }
                }

                // 1b. Process Pre-Event Alerts (Alert before event starts)
                if (calendarSettings.AlertBeforeEvent && calendarSettings.AlertBeforeEventMinutes > 0)
                {
                    var preAlertWindow = TimeSpan.FromMinutes(calendarSettings.AlertBeforeEventMinutes);
                    var preAlertDueEvents = events
                        .Where(e =>
                        {
                            var preAlertTime = e.Start - preAlertWindow;
                            return e.Start > now &&
                                   preAlertTime <= now &&
                                   preAlertTime >= now - missedThreshold &&
                                   !IsFiredPreAlert(e);
                        })
                        .OrderBy(e => e.Start)
                        .ToList();

                    foreach (var evt in preAlertDueEvents)
                    {
                        TriggerPreAlert(evt, calendarSettings.AlertBeforeEventMinutes);
                    }
                }

                // 2. Determine wait time for next event
                var nextEvent = events
                    .Where(e => e.Start > now)
                    .OrderBy(e => e.Start)
                    .FirstOrDefault();

                // Update next alert info for tray tooltip
                NextAlertTime = nextEvent?.Start;
                NextAlertDescription = nextEvent != null
                    ? (nextEvent.EventType == EventType.Interval ? "Interval" : nextEvent.Title)
                    : null;

                var heartbeatDelay = TimeSpan.FromSeconds(settings.HeartbeatIntervalSeconds);
                if (heartbeatDelay <= TimeSpan.Zero) heartbeatDelay = TimeSpan.FromSeconds(30); // Safety fallback

                TimeSpan waitTime = heartbeatDelay;

                if (nextEvent != null)
                {
                    var timeToEvent = nextEvent.Start - now;

                    // Also consider pre-alert time
                    if (calendarSettings.AlertBeforeEvent && calendarSettings.AlertBeforeEventMinutes > 0)
                    {
                        var timeToPreAlert = timeToEvent - TimeSpan.FromMinutes(calendarSettings.AlertBeforeEventMinutes);
                        if (timeToPreAlert > TimeSpan.Zero && timeToPreAlert < timeToEvent)
                        {
                            timeToEvent = timeToPreAlert;
                        }
                    }

                    if (timeToEvent < heartbeatDelay)
                    {
                        waitTime = timeToEvent;
                        if (waitTime < TimeSpan.Zero) waitTime = TimeSpan.Zero;
                    }
                }

                if (waitTime.TotalMilliseconds > 100) // Don't log spam for very short waits
                {
                   // _logger.LogDebug("Waiting for {WaitTime}", waitTime);
                }

                // Create a wake CTS that can be cancelled by OnSystemResumed
                _wakeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                try
                {
                    await WaitAsync(waitTime, _wakeCts.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // Woken by OnSystemResumed, loop immediately
                    _logger.LogInformation("Scheduler woken by system resume/time change");
                }
                finally
                {
                    _wakeCts.Dispose();
                    _wakeCts = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AlertScheduler loop.");
            // Wait a bit before restarting loop to avoid tight failure loop
            try { await WaitAsync(TimeSpan.FromSeconds(30), token); } catch { }
        }
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        await using var ctr = token.Register(() => tcs.TrySetCanceled());

        using var timer = _timeProvider.CreateTimer(state => 
        {
            var s = (TaskCompletionSource<bool>)state!;
            s.TrySetResult(true);
        }, tcs, delay, TimeSpan.Zero);

        await tcs.Task;
    }

    private void TriggerAlert(CalendarEvent evt)
    {
        _logger.LogInformation("Triggering alert for event: {Title} at {Start}", evt.Title, evt.Start);
        MarkAsFired(evt);
        MarkAsFiredPreAlert(evt); // Suppress pre-alert if regular alert already fired
        AlertTriggered?.Invoke(this, evt);
    }

    private void TriggerCatchUpAlert(CalendarEvent evt)
    {
        _logger.LogInformation("Triggering catch-up alert for event: {Title} at {Start}", evt.Title, evt.Start);
        MarkAsFired(evt);
        MarkAsFiredPreAlert(evt);

        var catchUpEvent = new CalendarEvent
        {
            EventType = evt.EventType,
            Uid = evt.Uid,
            SourceId = evt.SourceId,
            Title = $"{evt.Title} (catch-up)",
            Start = evt.Start,
            End = evt.End,
            Description = evt.Description,
            Location = evt.Location,
            IsAllDay = evt.IsAllDay,
            SourceName = evt.SourceName,
            OriginalStartTime = evt.OriginalStartTime,
            OriginalEndTime = evt.OriginalEndTime,
            OriginalTimeZoneId = evt.OriginalTimeZoneId
        };

        AlertTriggered?.Invoke(this, catchUpEvent);
    }

    private void TriggerPreAlert(CalendarEvent evt, int minutesBefore)
    {
        _logger.LogInformation("Triggering pre-alert for event: {Title} starting in {Minutes} minutes", evt.Title, minutesBefore);
        MarkAsFiredPreAlert(evt);

        var preAlertEvent = new CalendarEvent
        {
            Uid = $"prealert_{evt.Uid}_{evt.Start.Ticks}",
            SourceId = evt.SourceId,
            Title = $"{evt.Title} (in {minutesBefore} min)",
            Start = evt.Start,
            End = evt.End,
            Description = evt.Description,
            Location = evt.Location,
            IsAllDay = evt.IsAllDay
        };

        AlertTriggered?.Invoke(this, preAlertEvent);
    }

    public void OnSystemResumed()
    {
        _logger.LogInformation("System resumed or time changed, waking scheduler");
        try
        {
            _wakeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }
    }

    public void TriggerAlertManual(CalendarEvent evt)
    {
        _logger.LogInformation("Manually triggering alert for event: {Title}", evt.Title);
        AlertTriggered?.Invoke(this, evt);
    }

    private string GetKey(CalendarEvent evt)
    {
        return $"{evt.SourceId}_{evt.Uid}_{evt.Start.Ticks}";
    }

    private string GetPreAlertKey(CalendarEvent evt)
    {
        return $"prealert_{evt.SourceId}_{evt.Uid}_{evt.Start.Ticks}";
    }

    private bool IsFired(CalendarEvent evt)
    {
        return _firedEvents.ContainsKey(GetKey(evt));
    }

    private bool IsFiredPreAlert(CalendarEvent evt)
    {
        return _firedEvents.ContainsKey(GetPreAlertKey(evt));
    }

    private void MarkAsFired(CalendarEvent evt)
    {
        var key = GetKey(evt);
        if (!_firedEvents.ContainsKey(key))
        {
            _firedEvents[key] = evt.Start;
            SaveFiredEvents();
        }
    }

    private void MarkAsFiredPreAlert(CalendarEvent evt)
    {
        var key = GetPreAlertKey(evt);
        if (!_firedEvents.ContainsKey(key))
        {
            _firedEvents[key] = evt.Start;
            SaveFiredEvents();
        }
    }

    private void CleanupFiredEvents(DateTimeOffset now, TimeSpan threshold)
    {
        // Remove events that are older than threshold * 2 (just to be safe)
        var cutoff = now - (threshold * 2);
        var keysToRemove = _firedEvents.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();

        if (keysToRemove.Count == 0) return;

        foreach (var key in keysToRemove)
        {
            _firedEvents.Remove(key);
        }
        SaveFiredEvents();
    }

    private void LoadFiredEvents()
    {
        try
        {
            if (!File.Exists(_firedEventsPath)) return;

            var json = File.ReadAllText(_firedEventsPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
            if (data == null) return;

            foreach (var (key, ticks) in data)
            {
                _firedEvents[key] = new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            _logger.LogInformation("Loaded {Count} fired events from disk", _firedEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load fired events from disk, starting fresh");
        }
    }

    private void SaveFiredEvents()
    {
        try
        {
            var data = _firedEvents.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Ticks);
            var json = JsonSerializer.Serialize(data);
            var dir = Path.GetDirectoryName(_firedEventsPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_firedEventsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save fired events to disk");
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

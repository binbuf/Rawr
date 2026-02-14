using System;
using System.Collections.Generic;
using System.Linq;
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
    private Task? _schedulerTask;
    private readonly Dictionary<string, DateTimeOffset> _firedEvents = new(); // Key -> Event Start Time

    public event EventHandler<CalendarEvent>? AlertTriggered;

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

                var events = await _repository.GetAllEventsAsync(token);

                // 1. Process Due Events (Catch-up or Immediate)
                // Event is due if:
                // - Start <= Now
                // - Start >= Now - Threshold
                // - Not fired yet
                var dueEvents = events
                    .Where(e => 
                        e.Start <= now && 
                        e.Start >= now - missedThreshold &&
                        !IsFired(e))
                    .OrderBy(e => e.Start)
                    .ToList();

                foreach (var evt in dueEvents)
                {
                    TriggerAlert(evt);
                }

                // 2. Determine wait time for next event
                var nextEvent = events
                    .Where(e => e.Start > now)
                    .OrderBy(e => e.Start)
                    .FirstOrDefault();

                var heartbeatDelay = TimeSpan.FromSeconds(settings.HeartbeatIntervalSeconds);
                if (heartbeatDelay <= TimeSpan.Zero) heartbeatDelay = TimeSpan.FromSeconds(30); // Safety fallback

                TimeSpan waitTime = heartbeatDelay;

                if (nextEvent != null)
                {
                    var timeToEvent = nextEvent.Start - now;
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

                await WaitAsync(waitTime, token);
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
        AlertTriggered?.Invoke(this, evt);
    }

    private string GetKey(CalendarEvent evt)
    {
        return $"{evt.SourceId}_{evt.Uid}_{evt.Start.Ticks}";
    }

    private bool IsFired(CalendarEvent evt)
    {
        return _firedEvents.ContainsKey(GetKey(evt));
    }

    private void MarkAsFired(CalendarEvent evt)
    {
        var key = GetKey(evt);
        if (!_firedEvents.ContainsKey(key))
        {
            _firedEvents[key] = evt.Start;
        }
    }

    private void CleanupFiredEvents(DateTimeOffset now, TimeSpan threshold)
    {
        // Remove events that are older than threshold * 2 (just to be safe)
        var cutoff = now - (threshold * 2);
        var keysToRemove = _firedEvents.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
        
        foreach (var key in keysToRemove)
        {
            _firedEvents.Remove(key);
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

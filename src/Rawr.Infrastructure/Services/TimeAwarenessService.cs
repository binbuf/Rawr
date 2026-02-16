using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

public class TimeAwarenessService : ITimeAwarenessService, IDisposable
{
    private readonly ISettingsManager _settingsManager;
    private readonly IVoiceService _voiceService;
    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TimeAwarenessService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _serviceTask;

    public TimeAwarenessService(
        ISettingsManager settingsManager,
        IVoiceService voiceService,
        IAudioPlaybackService audioPlaybackService,
        TimeProvider timeProvider,
        ILogger<TimeAwarenessService> logger)
    {
        _settingsManager = settingsManager;
        _voiceService = voiceService;
        _audioPlaybackService = audioPlaybackService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_serviceTask != null && !_serviceTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serviceTask = RunServiceLoopAsync(_cts.Token);
        _logger.LogInformation("TimeAwarenessService started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_serviceTask != null)
            {
                try
                {
                    await _serviceTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            _cts.Dispose();
            _cts = null;
        }
        _logger.LogInformation("TimeAwarenessService stopped.");
    }

    private async Task RunServiceLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var settings = _settingsManager.Settings.TimeAwareness;
                if (!settings.Enabled)
                {
                    // Check again in a minute if enabled
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                    continue;
                }

                var now = _timeProvider.GetLocalNow();
                
                if (!IsWithinSchedule(now, settings))
                {
                    // Check again in a minute if we are now in schedule
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                    continue;
                }

                var nextInterval = CalculateNextInterval(now, settings.IntervalMinutes);
                var delay = nextInterval - now;

                if (delay.TotalMilliseconds > 0)
                {
                    _logger.LogDebug("TimeAwarenessService waiting for {Delay} until {NextInterval}", delay, nextInterval);
                    await Task.Delay(delay, token);
                }

                // Double check if still enabled and not cancelled
                if (token.IsCancellationRequested) break;
                
                // Reload settings to get fresh config
                settings = _settingsManager.Settings.TimeAwareness; 
                if (!settings.Enabled || !IsWithinSchedule(_timeProvider.GetLocalNow(), settings)) continue;

                await AnnounceTimeAsync(_timeProvider.GetLocalNow(), token);

                // Wait a tiny bit to avoid double triggering if calculation was slightly off (e.g. 1ms before minute)
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TimeAwarenessService loop.");
            try { await Task.Delay(TimeSpan.FromMinutes(1), token); } catch { }
        }
    }

    private bool IsWithinSchedule(DateTimeOffset now, TimeAwarenessConfig settings)
    {
        if (!settings.Enabled) return false;
        if (settings.Schedule == null || settings.Schedule.Count == 0) return true;

        var daySchedule = settings.Schedule.FirstOrDefault(s => s.Day == now.DayOfWeek);
        if (daySchedule == null || !daySchedule.IsEnabled) return false;

        var currentTime = now.TimeOfDay;
        
        var startTime = daySchedule.StartTime ?? TimeSpan.Zero;
        var endTime = daySchedule.EndTime ?? new TimeSpan(23, 59, 59);
        
        return currentTime >= startTime && currentTime <= endTime;
    }

    public async Task TriggerTimeAnnouncementManual(DateTimeOffset? simulatedTime = null)
    {
        await AnnounceTimeAsync(simulatedTime ?? _timeProvider.GetLocalNow(), CancellationToken.None);
    }

    private DateTimeOffset CalculateNextInterval(DateTimeOffset now, int intervalMinutes)
    {
        if (intervalMinutes <= 0) intervalMinutes = 60; // Default to hourly

        // Logic to find next interval (e.g. next hour, or next 15 min mark)
        // If interval is 60, we want top of the hour.
        // If interval is 15, we want 00, 15, 30, 45.
        
        var currentMinute = now.Minute;
        var minutesUntilNext = intervalMinutes - (currentMinute % intervalMinutes);
        
        // Target time
        var next = now.AddMinutes(minutesUntilNext);
        
        // Zero out seconds and milliseconds
        next = new DateTimeOffset(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0, next.Offset);

        // If the calculation puts us in the past or exactly now (due to seconds/ms adjustment), add interval
        if (next <= now)
        {
            next = next.AddMinutes(intervalMinutes);
        }

        return next;
    }

    private async Task AnnounceTimeAsync(DateTimeOffset now, CancellationToken token)
    {
        try
        {
            // Adjust "00" minutes to o'clock if needed
            // "It is 2 PM" is better than "It is 2 00 PM"
            string speechText;
            if (now.Minute == 0)
            {
                speechText = $"The time is {now:h} {now:tt}";
            }
            else
            {
                speechText = $"The time is {now:h mm tt}";
            }

            _logger.LogInformation("Announcing time: {SpeechText}", speechText);

            var voiceSettings = _settingsManager.Settings.Voice;
            if (voiceSettings.Muted)
            {
                _logger.LogInformation("Voice is muted, skipping announcement.");
                return;
            }

            var voiceOptions = new VoiceOptions
            {
                VoiceId = voiceSettings.VoiceId,
                Rate = voiceSettings.Rate,
                Volume = voiceSettings.Volume
            };

            using var audioStream = await _voiceService.SynthesizeAsync(speechText, voiceOptions);
            if (audioStream != null && audioStream.Length > 0)
            {
                if (audioStream.CanSeek) audioStream.Position = 0;
                await _audioPlaybackService.PlayAsync(audioStream, voiceSettings.DeviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to announce time.");
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

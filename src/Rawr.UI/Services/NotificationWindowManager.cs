using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using Rawr.Core.Services;
using Rawr.ViewModels;

namespace Rawr.Services;

public class NotificationWindowManager : IDisposable
{
    private readonly NotificationQueue _notificationQueue;
    private readonly IVoiceService _voiceService;
    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly ISettingsManager _settingsManager;
    private readonly IOsIntegrationService _osIntegrationService;
    private readonly IAlertScheduler _scheduler;
    private readonly ILogger<NotificationWindowManager> _logger;

    private NotificationWindow? _currentWindow;
    private CalendarEvent? _currentEvent;
    private readonly DispatcherTimer _fullscreenCheckTimer;
    private readonly Channel<CalendarEvent> _audioQueue = Channel.CreateBounded<CalendarEvent>(
        new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly CancellationTokenSource _audioQueueCts = new();

    public NotificationWindowManager(
        NotificationQueue notificationQueue,
        IVoiceService voiceService,
        IAudioPlaybackService audioPlaybackService,
        ISettingsManager settingsManager,
        IOsIntegrationService osIntegrationService,
        IAlertScheduler scheduler,
        ILogger<NotificationWindowManager> logger)
    {
        _notificationQueue = notificationQueue;
        _voiceService = voiceService;
        _audioPlaybackService = audioPlaybackService;
        _settingsManager = settingsManager;
        _osIntegrationService = osIntegrationService;
        _scheduler = scheduler;
        _logger = logger;

        _notificationQueue.AlertAdded += OnAlertAdded;
        _notificationQueue.AlertRemoved += OnAlertRemoved;

        _fullscreenCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _fullscreenCheckTimer.Tick += (s, e) => UpdateDisplay();
        _fullscreenCheckTimer.Start();

        _ = ProcessAudioQueueAsync(_audioQueueCts.Token);
    }

    private void OnAlertAdded(object? sender, CalendarEvent evt)
    {
        _logger.LogInformation("OnAlertAdded: {Uid} - {Title}", evt.Uid, evt.Title);
        // Queue sound for sequential playback
        _audioQueue.Writer.TryWrite(evt);

        // Update UI
        Dispatcher.UIThread.Post(UpdateDisplay);
    }

    private void OnAlertRemoved(object? sender, CalendarEvent evt)
    {
        Dispatcher.UIThread.Post(UpdateDisplay);
    }

    private void UpdateDisplay()
    {
        var activeAlerts = _notificationQueue.GetActiveAlerts();
        
        if (activeAlerts.Count == 0)
        {
            CloseCurrentWindow();
            return;
        }

        var config = _settingsManager.Settings.Notifications;
        if (config.HideOnFullscreen && _osIntegrationService.IsFullscreen())
        {
            CloseCurrentWindow();
            return;
        }

        if (config.RespectFocusAssist && _osIntegrationService.GetFocusAssistState() != FocusAssistState.Off)
        {
            CloseCurrentWindow();
            return;
        }

        // Get the oldest alert (FIFO) or maybe by time?
        // Let's go with the first one in the list which should be insertion order based on NotificationQueue logic
        var nextAlert = activeAlerts.First();

        if (_currentWindow != null)
        {
            if (_currentEvent != null && _currentEvent.Uid == nextAlert.Uid && _currentEvent.Start == nextAlert.Start)
            {
                // Same alert, do nothing
                return;
            }
            
            // Different alert is now top priority (e.g. previous one was dismissed)
            CloseCurrentWindow();
        }

        ShowAlert(nextAlert);
    }

    private void ShowAlert(CalendarEvent evt)
    {
        _currentEvent = evt;
        var config = _settingsManager.Settings.Notifications;
        var vm = new NotificationViewModel(evt, _notificationQueue, _settingsManager, _scheduler);
        _currentWindow = new NotificationWindow(vm, config);
        _currentWindow.Closed += (s, e) => 
        {
            // If window closed manually (e.g. Alt+F4), ensure it's dismissed from queue
            // But usually we want the Dismiss button to be the way.
            // If user closes window, we should probably dismiss it.
             _notificationQueue.Dismiss(evt);
             _currentWindow = null;
             _currentEvent = null;
        };
        _currentWindow.Show();

        // Auto-hide logic
        if (config.DurationSeconds > 0)
        {
            Task.Delay(TimeSpan.FromSeconds(config.DurationSeconds), _audioQueueCts.Token).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_currentWindow != null && _currentEvent == evt)
                    {
                        _notificationQueue.Dismiss(evt);
                    }
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private void CloseCurrentWindow()
    {
        if (_currentWindow != null)
        {
            _currentWindow.Close();
            _currentWindow = null;
            _currentEvent = null;
        }
    }

    private async Task ProcessAudioQueueAsync(CancellationToken token)
    {
        try
        {
            await foreach (var evt in _audioQueue.Reader.ReadAllAsync(token))
            {
                await PlayAlertSound(evt);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in audio queue processor");
        }
    }

    private async Task PlayAlertSound(CalendarEvent evt)
    {
        try
        {
            var voiceSettings = _settingsManager.Settings.Voice;
            if (voiceSettings.Muted)
            {
                return;
            }

            var notifConfig = _settingsManager.Settings.Notifications;
            if (notifConfig.RespectFocusAssistVoice && _osIntegrationService.GetFocusAssistState() != FocusAssistState.Off)
            {
                _logger.LogInformation("Voice suppressed due to Focus Assist / DND");
                return;
            }

            string text;
            if (evt.EventType == EventType.Interval)
            {
                // Time awareness interval alerts announce the time
                var t = evt.Start.LocalDateTime;
                text = t.Minute == 0
                    ? $"The time is {t.ToString("%h")} {t.ToString("tt")}"
                    : $"The time is {t.ToString("%h")} {t.ToString("mm")} {t.ToString("tt")}";
            }
            else
            {
                text = $"Reminder: {evt.Title}.";
                if (!evt.IsAllDay)
                {
                    text += $" at {evt.Start.LocalDateTime:t}";
                }
            }

            var options = new VoiceOptions
            {
                VoiceId = voiceSettings.VoiceId,
                Rate = voiceSettings.Rate,
                Volume = voiceSettings.Volume
            };

            _logger.LogInformation("PlayAlertSound synthesizing: {Text}", text);
            using var audio = await _voiceService.SynthesizeAsync(text, options);
            if (audio != null && audio.Length > 0)
            {
                _logger.LogInformation("PlayAlertSound playing audio stream ({Length} bytes)", audio.Length);
                await _audioPlaybackService.PlayAsync(audio, voiceSettings.DeviceId);
            }
            else
            {
                _logger.LogWarning("PlayAlertSound: synthesis returned null or empty stream");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PlayAlertSound");
        }
    }

    public void Dispose()
    {
        _fullscreenCheckTimer.Stop();
        _audioQueueCts.Cancel();
        _audioQueue.Writer.Complete();
        _audioQueueCts.Dispose();
        _notificationQueue.AlertAdded -= OnAlertAdded;
        _notificationQueue.AlertRemoved -= OnAlertRemoved;
        CloseCurrentWindow();
    }
}

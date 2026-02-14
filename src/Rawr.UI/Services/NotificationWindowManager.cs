using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
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
    
    private NotificationWindow? _currentWindow;
    private CalendarEvent? _currentEvent;

    public NotificationWindowManager(
        NotificationQueue notificationQueue,
        IVoiceService voiceService,
        IAudioPlaybackService audioPlaybackService,
        ISettingsManager settingsManager)
    {
        _notificationQueue = notificationQueue;
        _voiceService = voiceService;
        _audioPlaybackService = audioPlaybackService;
        _settingsManager = settingsManager;

        _notificationQueue.AlertAdded += OnAlertAdded;
        _notificationQueue.AlertRemoved += OnAlertRemoved;
    }

    private void OnAlertAdded(object? sender, CalendarEvent evt)
    {
        // Play sound for every new alert
        Task.Run(() => PlayAlertSound(evt));

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
        var vm = new NotificationViewModel(evt, _notificationQueue);
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
            Task.Delay(TimeSpan.FromSeconds(config.DurationSeconds)).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_currentWindow != null && _currentEvent == evt)
                    {
                        _notificationQueue.Dismiss(evt);
                    }
                });
            });
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

    private async Task PlayAlertSound(CalendarEvent evt)
    {
        try
        {
            var voiceSettings = _settingsManager.Settings.Voice;
            if (voiceSettings.Muted)
            {
                return;
            }

            string text = $"Reminder: {evt.Title}.";
            
            // If time is now or very close, say "Now".
            // If it's in future, say time?
            // "Meeting with Bob at 3 PM."
            if (!evt.IsAllDay)
            {
                text += $" at {evt.Start:t}";
            }

            var options = new VoiceOptions
            {
                VoiceId = voiceSettings.VoiceId,
                Rate = voiceSettings.Rate,
                Volume = voiceSettings.Volume
            };

            using var audio = await _voiceService.SynthesizeAsync(text, options);
            if (audio != null)
            {
                await _audioPlaybackService.PlayAsync(audio, voiceSettings.DeviceId);
            }
        }
        catch (Exception)
        {
            // Log error?
        }
    }

    public void Dispose()
    {
        _notificationQueue.AlertAdded -= OnAlertAdded;
        _notificationQueue.AlertRemoved -= OnAlertRemoved;
        CloseCurrentWindow();
    }
}

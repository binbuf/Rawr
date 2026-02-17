using Avalonia.Controls;
using Avalonia.Threading;
using System;
using Avalonia.Platform;
using Avalonia;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Services
{
    public class TrayIconService : IDisposable
    {
        private TrayIcon? _trayIcon;
        private readonly WindowIcon _idleIcon;
        private readonly WindowIcon _syncIcon;
        private readonly WindowIcon _alertIcon;

        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _tooltipTimer;
        private readonly ICalendarSyncService _syncService;
        private readonly IAlertScheduler _scheduler;
        private readonly ISettingsManager _settingsManager;

        private bool _isSyncing;
        private bool _isAlerting;
        private bool _toggleState;
        private DispatcherTimer? _alertResetTimer;

        public TrayIconService(ICalendarSyncService syncService, IAlertScheduler scheduler, ISettingsManager settingsManager)
        {
            _syncService = syncService;
            _scheduler = scheduler;
            _settingsManager = settingsManager;

            // Subscribe to events
            _syncService.IsSyncingChanged += OnSyncingChanged;
            _scheduler.AlertTriggered += OnAlertTriggered;

            // Load icons
            _idleIcon = LoadIcon("dinosaur.ico");
            _syncIcon = LoadIcon("calendar.ico");
            _alertIcon = LoadIcon("alert.ico");

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += Timer_Tick;

            _tooltipTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _tooltipTimer.Tick += (s, e) => UpdateTooltip();
            _tooltipTimer.Start();
        }

        public void Initialize(TrayIcon trayIcon)
        {
            _trayIcon = trayIcon;
            _trayIcon.Icon = _idleIcon; // Set initial icon
            UpdateState();
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            if (_trayIcon == null) return;

            if (_scheduler.NextAlertTime.HasValue && _scheduler.NextAlertDescription != null)
            {
                var localTime = _scheduler.NextAlertTime.Value.LocalDateTime;
                _trayIcon.ToolTipText = $"Rawr - Next: {localTime:t} ({_scheduler.NextAlertDescription})";
            }
            else
            {
                _trayIcon.ToolTipText = "Rawr";
            }
        }

        private void OnSyncingChanged(bool isSyncing)
        {
            Dispatcher.UIThread.Post(() => SetSyncing(isSyncing));
        }

        private void OnAlertTriggered(object? sender, CalendarEvent e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                SetAlerting(true);
                StartAlertResetTimer();
            });
        }

        private void StartAlertResetTimer()
        {
            // Stop any existing reset timer
            _alertResetTimer?.Stop();

            var durationSeconds = _settingsManager.Settings.Notifications.AlertFlashDurationSeconds;
            if (durationSeconds <= 0) return; // 0 means flash indefinitely until acknowledged

            _alertResetTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };
            _alertResetTimer.Tick += (s, e) =>
            {
                _alertResetTimer?.Stop();
                SetAlerting(false);
            };
            _alertResetTimer.Start();
        }

        public void AcknowledgeAlert()
        {
            _alertResetTimer?.Stop();
            SetAlerting(false);
        }

        private void SetSyncing(bool syncing)
        {
            _isSyncing = syncing;
            UpdateState();
        }

        private void SetAlerting(bool alerting)
        {
            _isAlerting = alerting;
            UpdateState();
        }

        private void UpdateState()
        {
            if (_trayIcon == null) return;

            if (_isAlerting || _isSyncing)
            {
                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                }
            }
            else
            {
                _timer.Stop();
                _trayIcon.Icon = _idleIcon;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_trayIcon == null) return;

            _toggleState = !_toggleState;

            if (_isAlerting)
            {
                // Priority: Alerting
                _trayIcon.Icon = _toggleState ? _alertIcon : _idleIcon;
            }
            else if (_isSyncing)
            {
                // Priority: Syncing
                _trayIcon.Icon = _toggleState ? _syncIcon : _idleIcon;
            }
        }

        private WindowIcon LoadIcon(string name)
        {
             var uri = new Uri($"avares://Rawr.UI/Assets/{name}");
             using var stream = AssetLoader.Open(uri);
             return new WindowIcon(stream);
        }

        public void Dispose()
        {
            _syncService.IsSyncingChanged -= OnSyncingChanged;
            _scheduler.AlertTriggered -= OnAlertTriggered;
            _alertResetTimer?.Stop();
            _timer.Stop();
            _tooltipTimer.Stop();
        }
    }
}

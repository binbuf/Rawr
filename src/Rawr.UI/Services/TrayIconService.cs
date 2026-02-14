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
        private readonly ICalendarSyncService _syncService;
        private readonly IAlertScheduler _scheduler;

        private bool _isSyncing;
        private bool _isAlerting;
        private bool _toggleState;

        public TrayIconService(ICalendarSyncService syncService, IAlertScheduler scheduler)
        {
            _syncService = syncService;
            _scheduler = scheduler;

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
        }

        public void Initialize(TrayIcon trayIcon)
        {
            _trayIcon = trayIcon;
            _trayIcon.Icon = _idleIcon; // Set initial icon
            UpdateState();
        }

        private void OnSyncingChanged(bool isSyncing)
        {
            Dispatcher.UIThread.Post(() => SetSyncing(isSyncing));
        }

        private void OnAlertTriggered(object? sender, CalendarEvent e)
        {
            Dispatcher.UIThread.Post(() => SetAlerting(true));
        }

        public void AcknowledgeAlert()
        {
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
            _timer.Stop();
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Rawr.Core.Interfaces;
using Rawr.Infrastructure.Configuration;
using Rawr.Infrastructure.Persistence;
using Rawr.Infrastructure.Services;
using Rawr.Core.Services;
using Rawr.Core.Models;
using Rawr.Services;
using Rawr.ViewModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Rawr
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current!;
        public IServiceProvider? Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Initialize TrayIconService
            try
            {
                var trayService = Services.GetRequiredService<TrayIconService>();
                
                // Create TrayIcon programmatically
                var trayIcon = new TrayIcon
                {
                    ToolTipText = "Rawr",
                    IsVisible = true,
                };
                trayIcon.Clicked += (s, e) => OnDashboardClick(s, e);
                
                // Create Menu
                trayIcon.Menu = CreateTrayMenu();

                // Add to application
                var icons = TrayIcon.GetIcons(this);
                if (icons == null)
                {
                    icons = new TrayIcons();
                    TrayIcon.SetIcons(this, icons);
                }
                icons.Add(trayIcon);

                trayService.Initialize(trayIcon);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize TrayIconService");
            }

            // Start Background Services
            var alertScheduler = Services.GetRequiredService<IAlertScheduler>();
            var timeAwareness = Services.GetRequiredService<ITimeAwarenessService>();
            var periodicSync = Services.GetRequiredService<PeriodicSyncService>();
            
            // Just resolve to instantiate and hook events
            Services.GetRequiredService<NotificationWindowManager>();

            _ = alertScheduler.StartAsync(CancellationToken.None);
            _ = timeAwareness.StartAsync(CancellationToken.None);
            _ = periodicSync.StartAsync(CancellationToken.None);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISettingsManager, SettingsManager>();
            services.AddLogging(logging => logging.AddSerilog());

            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<HttpClient>();
            services.AddSingleton<ICalendarParser, CalendarParser>();
            services.AddSingleton<ICalendarRepository, CalendarRepository>();
            services.AddSingleton<ICalendarSyncService, CalendarSyncService>();
            services.AddSingleton<IAlertScheduler, AlertScheduler>();
            services.AddSingleton<ITimeAwarenessService, TimeAwarenessService>();
            services.AddSingleton<PeriodicSyncService>();
            services.AddSingleton<NotificationQueue>();
            services.AddSingleton<NotificationWindowManager>();
            services.AddSingleton<TrayIconService>();

            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IVoiceService, WindowsVoiceService>();
                services.AddSingleton<IAudioPlaybackService, NAudioPlaybackService>();
                services.AddSingleton<IOsIntegrationService, WindowsOsIntegrationService>();
            }
            else
            {
                services.AddSingleton<IVoiceService, DummyVoiceService>();
                services.AddSingleton<IAudioPlaybackService, DummyPlaybackService>();
                services.AddSingleton<IOsIntegrationService, DummyOsIntegrationService>();
            }

            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();
        }

        private void OnDashboardClick(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow == null)
                {
                    var dashboardVm = Services?.GetRequiredService<DashboardViewModel>();
                    desktop.MainWindow = new DashboardWindow
                    {
                        DataContext = dashboardVm
                    };
                }
                
                desktop.MainWindow.Show();
                desktop.MainWindow.Activate();
                
                // Clear alerts when dashboard is opened
                var trayService = Services?.GetRequiredService<TrayIconService>();
                trayService?.AcknowledgeAlert();
            }
        }

        private void OnSyncNowClick(object? sender, EventArgs e)
        {
            var syncService = Services?.GetRequiredService<ICalendarSyncService>();
            _ = syncService?.SyncAsync(CancellationToken.None);
        }

        private SettingsWindow? _settingsWindow;

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (_settingsWindow != null)
                {
                    _settingsWindow.Activate();
                    return;
                }

                var settingsVm = Services?.GetRequiredService<SettingsViewModel>();
                if (settingsVm != null)
                {
                    _settingsWindow = new SettingsWindow
                    {
                        DataContext = settingsVm
                    };
                    _settingsWindow.Closed += (s, ev) => _settingsWindow = null;
                    _settingsWindow.Show();
                }
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is DashboardWindow mw)
                {
                    mw.CanClose = true;
                }
                desktop.Shutdown();
            }
        }

        private NativeMenu CreateTrayMenu()
        {
            var menu = new NativeMenu();

            var dashboardItem = new NativeMenuItem("Dashboard");
            dashboardItem.Click += OnDashboardClick;
            menu.Items.Add(dashboardItem);

            var syncItem = new NativeMenuItem("Sync Now");
            syncItem.Click += OnSyncNowClick;
            menu.Items.Add(syncItem);

            menu.Items.Add(new NativeMenuItemSeparator());

            // Snooze Submenu
            var snoozeMenu = new NativeMenu();
            var snoozeItem = new NativeMenuItem("Snooze") { Menu = snoozeMenu };
            
            var snoozeOptions = new (string Label, TimeSpan? Duration)[]
            {
                ("15 minutes", TimeSpan.FromMinutes(15)),
                ("30 minutes", TimeSpan.FromMinutes(30)),
                ("1 hour", TimeSpan.FromHours(1)),
                ("2 hours", TimeSpan.FromHours(2)),
                ("6 hours", TimeSpan.FromHours(6)),
                ("8 hours", TimeSpan.FromHours(8)),
                ("All day", null)
            };

            foreach (var opt in snoozeOptions)
            {
                var item = new NativeMenuItem(opt.Label);
                item.Click += (s, e) => {
                    var scheduler = Services?.GetRequiredService<IAlertScheduler>();
                    if (scheduler != null) {
                        if (opt.Duration.HasValue)
                            scheduler.SnoozeUntil = DateTimeOffset.Now.Add(opt.Duration.Value);
                        else
                            scheduler.SnoozeUntil = DateTimeOffset.Now.Date.AddDays(1);
                    }
                };
                snoozeMenu.Items.Add(item);
            }
            menu.Items.Add(snoozeItem);

            // Mute Voice Toggle
            var settingsManager = Services?.GetRequiredService<ISettingsManager>();
            var muteItem = new NativeMenuItem("Mute Voice") 
            { 
                IsChecked = settingsManager?.Settings.Voice.Muted ?? false
            };
            muteItem.Click += (s, e) => {
                if (settingsManager != null) {
                    settingsManager.Settings.Voice.Muted = !settingsManager.Settings.Voice.Muted;
                    settingsManager.Save();
                    muteItem.IsChecked = settingsManager.Settings.Voice.Muted;
                }
            };
            menu.Items.Add(muteItem);

            menu.Items.Add(new NativeMenuItemSeparator());

#if DEBUG
            // Debug Menu
            var debugMenu = new NativeMenu();
            var debugItem = new NativeMenuItem("Debug") { Menu = debugMenu };

            // Interval Based
            var intervalMenu = new NativeMenu();
            var intervalItem = new NativeMenuItem("Interval Based Alert") { Menu = intervalMenu };
            
            var next30 = new NativeMenuItem("Next 30 minute marker");
            next30.Click += (s, e) => SimulateIntervalAlert(30);
            intervalMenu.Items.Add(next30);
            
            var nextHour = new NativeMenuItem("Next hour");
            nextHour.Click += (s, e) => SimulateIntervalAlert(60);
            intervalMenu.Items.Add(nextHour);
            
            debugMenu.Items.Add(intervalItem);

            // Event Based
            var eventBasedMenu = new NativeMenu();
            var eventBasedItem = new NativeMenuItem("Event Based Alert") { Menu = eventBasedMenu };

            var refreshEventsItem = new NativeMenuItem("Refresh Events List");

            void PopulateEventMenu()
            {
                // Remove all items except the refresh item itself
                for (int i = eventBasedMenu.Items.Count - 1; i >= 0; i--)
                {
                    if (eventBasedMenu.Items[i] != refreshEventsItem)
                    {
                        eventBasedMenu.Items.RemoveAt(i);
                    }
                }
                eventBasedMenu.Items.Add(new NativeMenuItemSeparator());

                Task.Run(async () => {
                    var repo = Services?.GetRequiredService<ICalendarRepository>();
                    if (repo == null) return;

                    var evts = (await repo.GetAllEventsAsync(CancellationToken.None))
                                    .OrderBy(o => o.Start)
                                    .ToList();

                    Dispatcher.UIThread.Post(() => {
                        // Clear again in case of race
                        for (int i = eventBasedMenu.Items.Count - 1; i >= 0; i--)
                        {
                            if (eventBasedMenu.Items[i] != refreshEventsItem &&
                                eventBasedMenu.Items[i] is not NativeMenuItemSeparator)
                            {
                                eventBasedMenu.Items.RemoveAt(i);
                            }
                        }

                        if (evts.Count == 0)
                        {
                            eventBasedMenu.Items.Add(new NativeMenuItem("(No events found)"));
                            return;
                        }

                        foreach (var evt in evts)
                        {
                            var localStart = evt.Start.ToLocalTime();
                            var evtItem = new NativeMenuItem($"{localStart:t} - {evt.Title}");
                            evtItem.Click += (s2, e2) => {
                                var scheduler = Services?.GetRequiredService<IAlertScheduler>();
                                scheduler?.TriggerAlertManual(evt);
                            };
                            eventBasedMenu.Items.Add(evtItem);
                        }
                    });
                });
            }

            refreshEventsItem.Click += (s, e) => PopulateEventMenu();
            eventBasedMenu.Items.Add(refreshEventsItem);

            // Auto-populate on creation
            PopulateEventMenu();

            debugMenu.Items.Add(eventBasedItem);
            menu.Items.Add(debugItem);
#endif

            var settingsItem = new NativeMenuItem("Settings");
            settingsItem.Click += OnSettingsClick;
            menu.Items.Add(settingsItem);

            var exitItem = new NativeMenuItem("Exit");
            exitItem.Click += OnExitClick;
            menu.Items.Add(exitItem);

            return menu;
        }

        private void SimulateIntervalAlert(int minutes)
        {
            var timeAwareness = Services?.GetRequiredService<ITimeAwarenessService>();
            var scheduler = Services?.GetRequiredService<IAlertScheduler>();
            if (timeAwareness == null) return;

            var now = DateTimeOffset.Now;
            var currentMinute = now.Minute;
            var minutesUntilNext = minutes - (currentMinute % minutes);
            var next = now.AddMinutes(minutesUntilNext);
            
            next = new DateTimeOffset(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0, now.Offset);
            
            // Audio announcement
            _ = timeAwareness.TriggerTimeAnnouncementManual(next);

            // Visual notification (Meeting Alert)
            if (scheduler != null)
            {
                var dummyEvent = new CalendarEvent
                {
                    Uid = $"interval_{next.Ticks}",
                    Title = $"{next:t}",
                    Start = next,
                    End = next.AddMinutes(1),
                    Description = $"Triggered {minutes} minute interval marker."
                };
                scheduler.TriggerAlertManual(dummyEvent);
            }
        }
    }
}
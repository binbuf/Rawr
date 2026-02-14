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
using Rawr.Services;
using Rawr.ViewModels;
using Serilog;
using System;
using System.Net.Http;
using System.Threading;

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
                
                // Create Menu
                var menu = new NativeMenu();
                
                var dashboardItem = new NativeMenuItem("Dashboard");
                dashboardItem.Click += OnDashboardClick;
                menu.Items.Add(dashboardItem);

                var syncItem = new NativeMenuItem("Sync Now");
                syncItem.Click += OnSyncNowClick;
                menu.Items.Add(syncItem);

                menu.Items.Add(new NativeMenuItemSeparator());

                var settingsItem = new NativeMenuItem("Settings");
                settingsItem.Click += OnSettingsClick;
                menu.Items.Add(settingsItem);

                var exitItem = new NativeMenuItem("Exit");
                exitItem.Click += OnExitClick;
                menu.Items.Add(exitItem);

                trayIcon.Menu = menu;

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
            }
            else
            {
                services.AddSingleton<IVoiceService, DummyVoiceService>();
                services.AddSingleton<IAudioPlaybackService, DummyPlaybackService>();
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

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            // For now, just open dashboard as settings are part of it or not yet implemented
            OnDashboardClick(sender, e);
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
    }
}
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
            var trayService = Services.GetRequiredService<TrayIconService>();
            var trayIcons = TrayIcon.GetIcons(this);
            if (trayIcons != null && trayIcons.Count > 0)
            {
                trayService.Initialize(trayIcons[0]);
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
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
        }

        private void OnDashboardClick(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow == null)
                {
                    desktop.MainWindow = new MainWindow();
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
                if (desktop.MainWindow is MainWindow mw)
                {
                    mw.CanClose = true;
                }
                desktop.Shutdown();
            }
        }
    }
}
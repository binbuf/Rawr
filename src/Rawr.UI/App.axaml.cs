using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Rawr.Core.Interfaces;
using Rawr.Infrastructure.Configuration;
using Rawr.Infrastructure.Persistence;
using Rawr.Infrastructure.Services;
using Rawr.Core.Services;
using Serilog;
using System;
using System.Net.Http;

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

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
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
    }
}
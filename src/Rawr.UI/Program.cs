using Avalonia;
using System;
using System.Threading;
using Serilog;
using Serilog.Events;
using Rawr.Infrastructure.Configuration;

namespace Rawr
{
    internal class Program
    {
        private const string MutexName = "Rawr-SingleInstance-Mutex";

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) 
        {
            using var mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                // App is already running.
                return;
            }

            // Early initialization of settings to get log level
            var credentialProtection = OperatingSystem.IsWindows()
                ? (Core.Interfaces.ICredentialProtectionService)new Infrastructure.Services.WindowsCredentialProtectionService()
                : new Infrastructure.Services.DummyCredentialProtectionService();
            var settingsManager = new SettingsManager(credentialProtection);
            var config = settingsManager.Settings;

            // Map string level to Serilog level
            var level = Enum.TryParse<LogEventLevel>(config.Logging.Level, true, out var parsedLevel) 
                ? parsedLevel 
                : LogEventLevel.Information;

            // Windows: %APPDATA%\Rawr\logs
            // Others: ~/.config/Rawr/logs (via implementation of SettingsManager path logic)
            // We replicate the path logic here or use a known path. 
            // SettingsManager uses Environment.SpecialFolder.ApplicationData + "Rawr"
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Rawr", 
                "logs", 
                "rawr-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .WriteTo.Console()
                .WriteTo.Debug()
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: config.Logging.RetentionDays)
                .CreateLogger();

            try
            {
                Log.Information("Starting up");
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}

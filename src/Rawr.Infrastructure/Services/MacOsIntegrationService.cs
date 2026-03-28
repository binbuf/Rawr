using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

[SupportedOSPlatform("macos")]
public class MacOsIntegrationService : IOsIntegrationService
{
    private const string PlistLabel = "com.binbuf.rawr";
    private readonly ILogger<MacOsIntegrationService> _logger;

    public MacOsIntegrationService(ILogger<MacOsIntegrationService> logger)
    {
        _logger = logger;
    }

    public void SetStartWithOs(bool enable)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var launchAgentsDir = Path.Combine(home, "Library", "LaunchAgents");
            var plistPath = Path.Combine(launchAgentsDir, $"{PlistLabel}.plist");

            if (enable)
            {
                Directory.CreateDirectory(launchAgentsDir);

                var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Could not determine executable path for LaunchAgent");
                    return;
                }

                var plistContent = $"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                    <plist version="1.0">
                    <dict>
                        <key>Label</key>
                        <string>{PlistLabel}</string>
                        <key>ProgramArguments</key>
                        <array>
                            <string>{executablePath}</string>
                        </array>
                        <key>RunAtLoad</key>
                        <true/>
                    </dict>
                    </plist>
                    """;

                File.WriteAllText(plistPath, plistContent);
                _logger.LogInformation("Created LaunchAgent at {Path}", plistPath);
            }
            else
            {
                if (File.Exists(plistPath))
                {
                    File.Delete(plistPath);
                    _logger.LogInformation("Removed LaunchAgent at {Path}", plistPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} LaunchAgent", enable ? "create" : "remove");
        }
    }

    public bool IsFullscreen() => false;

    public FocusAssistState GetFocusAssistState()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var assertionsPath = Path.Combine(home, "Library", "DoNotDisturb", "DB", "Assertions.json");

            if (!File.Exists(assertionsPath))
                return FocusAssistState.Off;

            var json = File.ReadAllText(assertionsPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataArray) &&
                dataArray.GetArrayLength() > 0)
            {
                var first = dataArray[0];
                if (first.TryGetProperty("storeAssertionRecords", out var records) &&
                    records.GetArrayLength() > 0)
                {
                    return FocusAssistState.PriorityOnly;
                }
            }
        }
        catch
        {
            // Ignore errors reading DND state
        }

        return FocusAssistState.Off;
    }
}

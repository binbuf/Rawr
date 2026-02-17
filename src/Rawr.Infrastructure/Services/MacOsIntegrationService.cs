using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

[SupportedOSPlatform("macos")]
public class MacOsIntegrationService : IOsIntegrationService
{
    public void SetStartWithOs(bool enable)
    {
        // No-op for now — macOS launch agent setup can be added later
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

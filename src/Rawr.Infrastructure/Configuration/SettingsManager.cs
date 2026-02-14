using System;
using System.IO;
using System.Text.Json;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;

namespace Rawr.Infrastructure.Configuration;

public class SettingsManager : ISettingsManager
{
    private readonly string _configPath;
    private readonly string _appDataPath;

    public RawrConfig Settings { get; private set; } = new();

    public SettingsManager(string? basePath = null)
    {
        string baseFolder = basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _appDataPath = Path.Combine(baseFolder, "Rawr");
        _configPath = Path.Combine(_appDataPath, "settings.json");

        Load();
    }

    public void Load()
    {
        if (!File.Exists(_configPath))
        {
            Settings = new RawrConfig();
            Save(); // Create default file
            return;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            Settings = JsonSerializer.Deserialize<RawrConfig>(json, options) ?? new RawrConfig();
        }
        catch (Exception)
        {
            // If the file is corrupted, we might want to backup and reset, or just reset.
            // For now, reset to defaults.
            Settings = new RawrConfig();
        }
    }

    public void Save()
    {
        if (!Directory.Exists(_appDataPath))
        {
            Directory.CreateDirectory(_appDataPath);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(Settings, options);
        File.WriteAllText(_configPath, json);
    }
}

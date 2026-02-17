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
    private readonly ICredentialProtectionService _credentialProtection;

    public RawrConfig Settings { get; private set; } = new();
    public string AppDataPath => _appDataPath;

    public SettingsManager(ICredentialProtectionService credentialProtection, string? basePath = null)
    {
        _credentialProtection = credentialProtection;
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

            // Decrypt calendar source URIs
            foreach (var source in Settings.Calendar.Sources)
            {
                source.Uri = _credentialProtection.Unprotect(source.Uri);
            }
        }
        catch (Exception)
        {
            Settings = new RawrConfig();
        }
    }

    public void Save()
    {
        if (!Directory.Exists(_appDataPath))
        {
            Directory.CreateDirectory(_appDataPath);
        }

        // Encrypt calendar source URIs before saving
        foreach (var source in Settings.Calendar.Sources)
        {
            source.Uri = _credentialProtection.Protect(source.Uri);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(Settings, options);
        File.WriteAllText(_configPath, json);

        // Decrypt back so in-memory values remain usable
        foreach (var source in Settings.Calendar.Sources)
        {
            source.Uri = _credentialProtection.Unprotect(source.Uri);
        }
    }
}

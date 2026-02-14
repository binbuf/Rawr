using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;

namespace Rawr.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsManager _settingsManager;
    private readonly IVoiceService _voiceService;
    private readonly IAudioPlaybackService _audioPlaybackService;

    public SettingsViewModel(
        ISettingsManager settingsManager,
        IVoiceService voiceService,
        IAudioPlaybackService audioPlaybackService)
    {
        _settingsManager = settingsManager;
        _voiceService = voiceService;
        _audioPlaybackService = audioPlaybackService;
        
        LoadSettings();
    }
    
    // Default constructor for design-time
    public SettingsViewModel()
    {
        _settingsManager = null!;
        _voiceService = null!;
        _audioPlaybackService = null!;
    }

    [ObservableProperty]
    private RawrConfig _config = new();

    [ObservableProperty]
    private ObservableCollection<CalendarSource> _calendarSources = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableVoices = new();

    private void LoadSettings()
    {
        if (_settingsManager == null) return;
        
        Config = _settingsManager.Settings;
        CalendarSources = new ObservableCollection<CalendarSource>(Config.Calendar.Sources);
        
        var voices = _voiceService.GetInstalledVoices();
        AvailableVoices = new ObservableCollection<string>(voices.Select(v => v.Name));
    }

    [RelayCommand]
    private void Save()
    {
        // Config is already a reference to _settingsManager.Settings if we assigned it correctly
        // But to be safe, we can copy back if needed. 
        // Assuming Config is bound to UI and updates the object.
        
        // _settingsManager.Settings is likely the same instance as Config.
        // If not, we might need to copy properties.
        // Since we did: Config = _settingsManager.Settings; in LoadSettings, it is the same reference.
        
        Config.Calendar.Sources = CalendarSources.ToList();
        _settingsManager.Save();
    }

    [RelayCommand]
    private void AddSource()
    {
        CalendarSources.Add(new CalendarSource { Name = "New Source", Uri = "https://" });
    }

    [RelayCommand]
    private void RemoveSource(CalendarSource source)
    {
        if (source != null)
        {
            CalendarSources.Remove(source);
        }
    }

    [RelayCommand]
    private async Task TestVoice()
    {
        try
        {
            var options = new VoiceOptions
            {
                VoiceId = Config.Voice.VoiceId,
                Rate = Config.Voice.Rate,
                Volume = Config.Voice.Volume
            };

            using var stream = await _voiceService.SynthesizeAsync("This is a test of the selected voice.", options);
            if (stream != null && stream.Length > 0)
            {
                await _audioPlaybackService.PlayAsync(stream, Config.Voice.DeviceId);
            }
        }
        catch (Exception)
        {
            // Ignore for now
        }
    }
}

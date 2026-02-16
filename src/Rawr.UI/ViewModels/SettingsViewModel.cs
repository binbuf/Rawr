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
    private readonly IOsIntegrationService _osIntegrationService;
    private readonly ICalendarRepository _calendarRepository;

    public SettingsViewModel(
        ISettingsManager settingsManager,
        IVoiceService voiceService,
        IAudioPlaybackService audioPlaybackService,
        IOsIntegrationService osIntegrationService,
        ICalendarRepository calendarRepository)
    {
        _settingsManager = settingsManager;
        _voiceService = voiceService;
        _audioPlaybackService = audioPlaybackService;
        _osIntegrationService = osIntegrationService;
        _calendarRepository = calendarRepository;
        
        LoadSettings();
    }
    
    // Default constructor for design-time
    public SettingsViewModel()
    {
        _settingsManager = null!;
        _voiceService = null!;
        _audioPlaybackService = null!;
        _osIntegrationService = null!;
        _calendarRepository = null!;
    }

    [ObservableProperty]
    private RawrConfig _config = new();

    [ObservableProperty]
    private ObservableCollection<CalendarSource> _calendarSources = new();

    [ObservableProperty]
    private ObservableCollection<DaySchedule> _intervalSchedule = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableVoices = new();

    [ObservableProperty]
    private ObservableCollection<PopupPosition> _availablePopupPositions = new(Enum.GetValues<PopupPosition>());

    private void LoadSettings()
    {
        if (_settingsManager == null) return;
        
        Config = _settingsManager.Settings;
        CalendarSources = new ObservableCollection<CalendarSource>(Config.Calendar.Sources);
        IntervalSchedule = new ObservableCollection<DaySchedule>(Config.TimeAwareness.Schedule);
        
        var voices = _voiceService.GetInstalledVoices();
        AvailableVoices = new ObservableCollection<string>(voices.Select(v => v.Name));
    }

    [RelayCommand]
    private void Save()
    {
        Config.Calendar.Sources = CalendarSources.ToList();
        Config.TimeAwareness.Schedule = IntervalSchedule.ToList();
        _settingsManager.Save();
        _osIntegrationService.SetStartWithOs(Config.General.StartWithOS);
    }

    [RelayCommand]
    private async Task ResetData()
    {
        await _calendarRepository.ClearAllEventsAsync();
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

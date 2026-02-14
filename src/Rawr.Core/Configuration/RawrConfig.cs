using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rawr.Core.Configuration;

public partial class RawrConfig : ObservableObject
{
    [ObservableProperty]
    private GeneralConfig _general = new();

    [ObservableProperty]
    private TimeAwarenessConfig _timeAwareness = new();

    [ObservableProperty]
    private CalendarConfig _calendar = new();

    [ObservableProperty]
    private VoiceConfig _voice = new();

    [ObservableProperty]
    private LoggingConfig _logging = new();

    [ObservableProperty]
    private LinuxConfig _linux = new();
}

public partial class GeneralConfig : ObservableObject
{
    [ObservableProperty]
    private bool _startWithOS = true;

    [ObservableProperty]
    private int _missedEventThresholdMinutes = 60;

    [ObservableProperty]
    private int _heartbeatIntervalSeconds = 30;
}

public partial class TimeAwarenessConfig : ObservableObject
{
    [ObservableProperty]
    private bool _enabled = false;

    [ObservableProperty]
    private int _intervalMinutes = 60;

    [ObservableProperty]
    private string _sound = "Chime"; // or "Voice"
}

public partial class CalendarConfig : ObservableObject
{
    [ObservableProperty]
    private int _lookAheadHours = 48;

    [ObservableProperty]
    private List<CalendarSource> _sources = new();

    [ObservableProperty]
    private int _syncIntervalMinutes = 15;
}

public partial class CalendarSource : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = "Personal";

    [ObservableProperty]
    private string _uri = string.Empty;

    [ObservableProperty]
    private string _type = "Remote"; 

    [ObservableProperty]
    private string _authType = "PrivateUrl"; // "PrivateUrl" or "Basic"

    [ObservableProperty]
    private string _color = "#FF0000";

    [ObservableProperty]
    private bool _enabled = true;
}

public partial class VoiceConfig : ObservableObject
{
    [ObservableProperty]
    private string _engine = "Auto";

    [ObservableProperty]
    private string _voiceId = "Default";

    [ObservableProperty]
    private string _deviceId = "Default";

    [ObservableProperty]
    private double _rate = 1.0;

    [ObservableProperty]
    private int _volume = 100;

    [ObservableProperty]
    private bool _muted = false;
}

public partial class LoggingConfig : ObservableObject
{
    [ObservableProperty]
    private string _level = "Information";

    [ObservableProperty]
    private int _retentionDays = 7;
}

public partial class LinuxConfig : ObservableObject
{
    [ObservableProperty]
    private bool _forceCustomWindow = false;

    [ObservableProperty]
    private string _notificationUrgency = "Critical";
}

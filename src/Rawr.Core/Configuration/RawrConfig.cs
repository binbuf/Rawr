using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rawr.Core.Configuration;

public enum PopupPosition
{
    BottomRight,
    TopRight,
    TopLeft,
    BottomLeft
}

public partial class RawrConfig : ObservableObject
{
    [ObservableProperty]
    private GeneralConfig _general = new();

    [ObservableProperty]
    private NotificationConfig _notifications = new();

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

    [ObservableProperty]
    private int _gracePeriodSeconds = 120;
}

public partial class NotificationConfig : ObservableObject
{
    [ObservableProperty]
    private PopupPosition _position = PopupPosition.BottomRight;

    [ObservableProperty]
    private int _durationSeconds = 10;

    [ObservableProperty]
    private bool _hideOnFullscreen = true;

    [ObservableProperty]
    private int _alertFlashDurationSeconds = 10;

    [ObservableProperty]
    private bool _showLocalTimezone = true;

    [ObservableProperty]
    private int _defaultSnoozeMinutes = 10;

    [ObservableProperty]
    private bool _respectFocusAssist = true;

    [ObservableProperty]
    private bool _respectFocusAssistVoice = false;
}

public partial class TimeAwarenessConfig : ObservableObject
{
    [ObservableProperty]
    private bool _enabled = false;

    [ObservableProperty]
    private int _intervalMinutes = 60;

    [ObservableProperty]
    private string _sound = "Chime"; // or "Voice"

    [ObservableProperty]
    private List<DaySchedule> _schedule = new()
    {
        new() { Day = DayOfWeek.Monday, IsEnabled = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
        new() { Day = DayOfWeek.Tuesday, IsEnabled = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
        new() { Day = DayOfWeek.Wednesday, IsEnabled = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
        new() { Day = DayOfWeek.Thursday, IsEnabled = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
        new() { Day = DayOfWeek.Friday, IsEnabled = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
        new() { Day = DayOfWeek.Saturday, IsEnabled = false, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
        new() { Day = DayOfWeek.Sunday, IsEnabled = false, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
    };
}

public partial class DaySchedule : ObservableObject
{
    [ObservableProperty]
    private DayOfWeek _day;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private TimeSpan? _startTime;

    [ObservableProperty]
    private TimeSpan? _endTime;
}

public partial class CalendarConfig : ObservableObject
{
    [ObservableProperty]
    private int _lookAheadHours = 48;

    [ObservableProperty]
    private List<CalendarSource> _sources = new();

    [ObservableProperty]
    private int _syncIntervalMinutes = 15;

    [ObservableProperty]
    private bool _alertBeforeEvent = false;

    [ObservableProperty]
    private int _alertBeforeEventMinutes = 15;
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

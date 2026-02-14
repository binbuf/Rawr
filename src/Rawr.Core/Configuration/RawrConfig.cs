using System;
using System.Collections.Generic;

namespace Rawr.Core.Configuration;

public class RawrConfig
{
    public GeneralConfig General { get; set; } = new();
    public TimeAwarenessConfig TimeAwareness { get; set; } = new();
    public CalendarConfig Calendar { get; set; } = new();
    public VoiceConfig Voice { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public LinuxConfig Linux { get; set; } = new();
}

public class GeneralConfig
{
    public bool StartWithOS { get; set; } = true;
    public int MissedEventThresholdMinutes { get; set; } = 60;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
}

public class TimeAwarenessConfig
{
    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 60;
    public string Sound { get; set; } = "Chime"; // or "Voice"
}

public class CalendarConfig
{
    public int LookAheadHours { get; set; } = 48;
    public List<CalendarSource> Sources { get; set; } = new();
    public int SyncIntervalMinutes { get; set; } = 15;
}

public class CalendarSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Personal";
    public string Uri { get; set; } = string.Empty;
    public string Type { get; set; } = "Remote"; 
    public string AuthType { get; set; } = "PrivateUrl"; // "PrivateUrl" or "Basic"
    public string Color { get; set; } = "#FF0000";
    public bool Enabled { get; set; } = true;
}

public class VoiceConfig
{
    public string Engine { get; set; } = "Auto";
    public string VoiceId { get; set; } = "Default";
    public string DeviceId { get; set; } = "Default";
    public double Rate { get; set; } = 1.0;
    public int Volume { get; set; } = 100;
    public bool Muted { get; set; } = false;
}

public class LoggingConfig
{
    public string Level { get; set; } = "Information";
    public int RetentionDays { get; set; } = 7;
}

public class LinuxConfig
{
    public bool ForceCustomWindow { get; set; } = false;
    public string NotificationUrgency { get; set; } = "Critical";
}

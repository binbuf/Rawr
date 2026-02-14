# Rawr - Design Document

## 1. Overview
`Rawr` is a cross-platform desktop application built with Avalonia UI. It resides primarily in the system tray and provides voice-synthesized alerts and popup notifications for calendar events and scheduled intervals.

## 2. Technology Stack
- **Framework:** Avalonia UI (Cross-platform XAML)
- **Runtime:** .NET 8.0+
- **ICS Parsing:** `Ical.Net` (Standard library for iCalendar handling)
- **Storage:** JSON (System.Text.Json) for settings and event caching.
- **Voice Synthesis:** Native OS API Abstraction (`IVoiceService`)
  - **Windows:** `System.Speech` or `Windows.Media.SpeechSynthesis`
  - **macOS:** `NSSpeechSynthesizer` (via interop)
  - **Linux:** `speech-dispatcher` or `espeak`

## 3. Architecture

### 3.1 Components
- **Main Application:** Handles lifecycle and tray icon management.
- **Background Services:**
  - `CalendarSyncService`: Periodically fetches and parses `.ics` files (web or local). Rotates events to keep only the next few days.
  - `AlertScheduler`: Monitors time and triggers alerts based on scheduled intervals and calendar events.
  - `VoiceService`: Provides platform-specific TTS.
  - `SettingsManager`: Handles persistence of user configuration.
- **UI Windows:**
  - `SettingsWindow`: Tabbed interface for configuration.
  - `NotificationPopup`: A lightweight, borderless window that appears for alerts.

### 3.2 Data Flow
1. **Sync:** `CalendarSyncService` fetches data -> `Ical.Net` parses -> `SettingsManager` caches events in JSON.
2. **Alert:** `AlertScheduler` checks current time against `Schedule` and `CachedEvents`.
3. **Trigger:** `AlertScheduler` invokes `NotificationPopup` and `VoiceService`.

## 4. Data Models

### 4.1 Configuration (JSON)
```json
{
  "General": {
    "StartWithOS": true,
    "LogLevel": "Information",
    "PopupDurationSeconds": 10,
    "DefaultSnoozeMinutes": 60,
    "SkipVisualOnFullscreen": true
  },
  "Schedule": {
    "Days": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
    "StartTime": "08:00:00",
    "EndTime": "18:00:00",
    "IntervalMinutes": 30
  },
  "Calendar": {
    "Enabled": true,
    "ReminderOffsets": [0, 5, 15],
    "Sources": [
      {
        "Type": "Web",
        "Url": "https://example.com/cal.ics",
        "RefreshInterval": 60
      }
    ]
  },
  "Voice": {
    "VoiceId": "Default",
    "Rate": 1.0,
    "Volume": 100,
    "Muted": false
  }
}
```

## 5. Implementation Details

### 5.1 System Tray Integration
- Use `Avalonia.Controls.TrayIcon`.
- Menu options: `Settings`, `Snooze (Global)`, `Sync Now`, `Exit`.
- `MainWindow` will be hidden by default (`IsVisible="False"`) or used only for the Settings view.

### 5.2 Alert Logic
- **Interval Alerts:** Triggered every `n` minutes within the `Start` and `End` time window on selected `Days`.
- **Calendar Alerts:** Triggered `m` minutes before an event starts.
- **Logic:** A 1-second timer checks the next scheduled alert.

### 5.3 Voice Synthesis Abstraction
```csharp
public interface IVoiceService {
    Task SpeakAsync(string text, VoiceSettings settings);
    IEnumerable<VoiceInfo> GetAvailableVoices();
}
```

### 5.4 Popup Notification
- Top-most window, positioned at the bottom-right (or platform equivalent).
- Display current time and event summary.
- Snooze button: Disables the specific alert for the configured duration.

## 6. Challenges & Solutions
- **Fullscreen Detection:** Use platform-specific APIs (e.g., `GetForegroundWindow` + `GetWindowRect` on Windows) to detect if a fullscreen app is active.
- **Cross-platform TTS:** Implement platform-specific backends for `IVoiceService`.
- **Sync Rotation:** `CalendarSyncService` will purge events older than 1 day and only cache up to 7 days forward to keep the JSON footprint small.

## 7. Future Considerations
- Support for multiple schedules.
- Custom voice prompt templates (e.g., "Hey! It's {time}").
- Integration with OS level "Do Not Disturb" modes.

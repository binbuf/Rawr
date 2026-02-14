# Rawr - Design Document

## 1. Overview
`Rawr` is a cross-platform desktop application built with Avalonia UI. It resides primarily in the system tray and provides voice-synthesized alerts and popup notifications for calendar events and scheduled intervals.

## 2. Technology Stack
- **Framework:** Avalonia UI (Cross-platform XAML)
- **Runtime:** .NET 10.0
- **ICS Parsing:** `Ical.Net` (Standard library for iCalendar handling)
- **Storage:** JSON (System.Text.Json) for settings and event caching.
- **Concurrency:** `System.Threading.Channels` or `SemaphoreSlim` for safe repository access.
- **Voice Synthesis:** Native OS API Abstraction (`IVoiceService`)
  - **Windows:** `System.Speech` or `Windows.Media.SpeechSynthesis`
  - **macOS:** `NSSpeechSynthesizer` (via interop)
  - **Linux:** `speech-dispatcher` or `espeak`

## 3. Architecture

### 3.1 Components
- **Main Application:** Handles lifecycle, single-instance check, and tray icon management.
- **Core Services:**
  - `CalendarRepository`: **(Singleton)** Thread-safe in-memory store for events. Handles loading/saving to JSON but serves queries from memory.
  - `IOsIntegrationService`: Abstraction for OS-specific tasks (Start on Boot, Fullscreen detection).
  - `CalendarSyncService`: Background service that fetches/parses `.ics` files and updates the `CalendarRepository`.
  - `AlertScheduler`: specific "Dynamic Timer" logic (not polling). Calculates time to next alert and sleeps until then.
  - `VoiceService`: Provides platform-specific TTS.
  - `SettingsManager`: Handles persistence of user configuration.
- **UI Windows:**
  - `DashboardWindow`: Main interface showing upcoming events and status.
  - `SettingsWindow`: Tabbed interface for configuration.
  - `NotificationPopup`: A lightweight, borderless window. Configured as `ShowActivated = false` to prevent focus stealing.

### 3.2 Data Flow
1. **Sync:** `CalendarSyncService` fetches data -> parses via `Ical.Net` -> updates `CalendarRepository` (write lock).
   - *On Failure:* Emits failure event -> Updates `SettingsManager` status -> Triggers Error Popup.
2. **Persistence:** `CalendarRepository` flushes changes to `events.json` asynchronously.
3. **Scheduling:** `AlertScheduler` queries `CalendarRepository` (read lock) for the next upcoming event.
4. **Trigger:** `AlertScheduler` wakes up -> invokes `NotificationPopup` & `VoiceService` -> updates `TrayIcon` animation.

## 4. Data Models

### 4.1 Configuration (JSON)
```json
{
  "General": {
    "StartWithOS": true,
    "LogLevel": "Information",
    "PopupDurationSeconds": 10,
    "DefaultSnoozeMinutes": 10,
    "SkipVisualOnFullscreen": true,
    "TrayAnimationIntervalMs": 300,
    "TrayAnimationDurationMs": 5000
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

### 5.1 Application Lifecycle
- **Single Instance:** `Program.cs` must use a `Mutex` or `NamedPipe`.
- **Startup:**
  1. Load Settings & Repository.
  2. Initialize Tray Icon.
  3. Start `CalendarSyncService` and `AlertScheduler`.
  4. Application runs in background (Tray only initially).
- **Tray Icon Interactions:**
  - **Left Click:** Opens `DashboardWindow` (Upcoming Events).
  - **Right Click:** Opens Context Menu (`Settings`, `Sync Now`, `Exit`).

### 5.2 OS Integration (`IOsIntegrationService`)
- **Start With OS:**
  - **Windows:** Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  - **macOS:** `LaunchAgent` (.plist)
  - **Linux:** `.desktop` file in `~/.config/autostart`
- **Fullscreen Detection:**
  - **Windows:** `GetForegroundWindow` + `GetWindowRect` user32.dll calls.

### 5.3 Alert Logic
- **Dynamic Scheduling:** Sleep/Delay until `nextAlertTime`.
- **Missed Alerts:**
  - Upon wake, if alert > 15 mins old, log only.
  - If < 15 mins, trigger immediately.

### 5.4 Popup Notification (UI/UX)
- **Window Properties:** `Topmost=true`, `SystemDecorations=None`, `ShowInTaskbar=false`.
- **Focus:** CRITICAL: Must use `ShowActivated = false` (Win32 `SW_SHOWNOACTIVATE`).
- **Interaction:**
  - **Snooze Button:** Dismisses popup, reschedules alert for `DefaultSnoozeMinutes` later. Closes window immediately.
  - **Dismiss Button:** Marks alert as handled. Closes window immediately.
  - **Content:** Event Title, Time remaining/Time of event, Source Calendar name.

### 5.5 Dashboard View (New)
- **Purpose:** Provide a quick overview of the day without digging into settings.
- **UI Elements:**
  - List of upcoming events for the day (sorted chronologically).
  - "Sync Now" button.
  - "Settings" button (link to SettingsWindow).
  - Visual indicator of current status (e.g., "Next alert in 15 mins").

### 5.6 Error Handling & Visibility
- **Sync Failures:**
  - **Immediate:** Trigger a specific "Sync Failed" popup notification (distinct style from event alerts).
  - **Persistent:** Update `Calendar` tab in `SettingsWindow` to show "Last Sync Attempt: [Timestamp]" and "Status: Failed ([Reason])".
  - **Retry:** Background service should employ exponential backoff.

### 5.7 Voice Synthesis Abstraction
```csharp
public interface IVoiceService {
    Task SpeakAsync(string text, VoiceSettings settings);
    IEnumerable<VoiceInfo> GetAvailableVoices();
}
```

## 6. Challenges & Solutions
- **Focus Stealing:** Solved by platform-specific window flags (`ShowActivated=false`).
- **Cross-platform TTS:** Implement platform-specific backends for `IVoiceService`.
- **Sync Rotation:** `CalendarSyncService` will purge events older than 1 day and only cache up to 7 days forward.

## 7. Future Considerations
- Support for multiple schedules.
- Custom voice prompt templates (e.g., "Hey! It's {time}").
- Integration with OS level "Do Not Disturb" modes.

# Rawr - Design Document

## 1. Overview
`Rawr` is a cross-platform desktop application built with Avalonia UI. It resides primarily in the system tray and provides voice-synthesized alerts and popup notifications for calendar events and scheduled intervals.

## 2. Technology Stack
- **Framework:** Avalonia UI (Cross-platform XAML)
- **Runtime:** .NET 10.0
- **DI Container:** `Microsoft.Extensions.DependencyInjection`
- **ICS Parsing:** `Ical.Net` (Standard library for iCalendar handling)
- **Storage:** JSON (System.Text.Json) for settings and event caching.
- **Logging:** Serilog (Rolling file sink, configurable).
- **Concurrency:** `System.Threading.Channels` or `SemaphoreSlim` for safe repository access.
- **Voice Synthesis:** Native OS API Abstraction (`IVoiceService`)
  - **Windows:** Hybrid `Windows.Media.SpeechSynthesis` (WinRT) with `System.Speech` (SAPI) fallback.
  - **macOS:** `NSSpeechSynthesizer` (via interop)
  - **Linux:** `speech-dispatcher` or `espeak` via CLI/library.

## 3. Architecture

### 3.1 Components
- **Main Application:** Handles lifecycle, single-instance check, and tray icon management. Bootstrap via `Host.CreateDefaultBuilder`.
- **Core Services:**
  - `CalendarRepository`: **(Singleton)** Thread-safe in-memory store for events. Handles loading/saving to JSON.
  - `IOsIntegrationService`: Abstraction for OS-specific tasks (Start on Boot, Fullscreen detection, File Paths).
  - `CalendarSyncService`: Background service that fetches `.ics` streams (Local/Remote, Auth support), parses them, and updates the `CalendarRepository`.
  - `AlertScheduler`: **(Singleton)** Manages the "Next Alert" logic. Handles system sleep/wake resilience.
  - `VoiceService`: Provides platform-specific TTS.
  - `SettingsManager`: Handles persistence of user configuration.
  - `NotificationQueue`: **(Singleton)** Manages the serialization of popup alerts.
- **UI Windows:**
  - `DashboardWindow`: Main interface showing upcoming events and status.
  - `SettingsWindow`: Tabbed interface for configuration.
  - `NotificationPopup`: Custom borderless window (Windows/macOS/X11).
  - *Linux Wayland Strategy:* Uses `libnotify` (DBus) instead of custom window to ensure visibility and protocol compliance.

### 3.2 Data Flow
1. **Sync:** `CalendarSyncService` fetches data -> parses via `Ical.Net` -> updates `CalendarRepository`.
2. **Persistence:** `CalendarRepository` flushes changes to `events.json`.
3. **Scheduling:** `AlertScheduler` calculates time to next event.
   - **Resilience:** Uses `SystemEvents.PowerModeChanged` (Win/Linux/Mac abstractions) and a `PeriodicTimer` (Heartbeat, 30s) to detect sleep/wake cycles.
4. **Trigger:** `AlertScheduler` wakes up -> pushes alert to `NotificationQueue`.
   - **Filter:** If event is older than `MissedEventThreshold` (default 60m), it is logged and skipped.

### 3.3 File System & Paths
Storage locations follow platform standards:
- **Windows:** `%APPDATA%\Rawr` (e.g., `C:\Users\Dan\AppData\Roaming\Rawr`)
- **macOS:** `~/Library/Application Support/Rawr`
- **Linux:**
  - Config: `$XDG_CONFIG_HOME/rawr` (default `~/.config/rawr`)
  - Data: `$XDG_DATA_HOME/rawr` (default `~/.local/share/rawr`)

### 3.4 Diagnostics & Logging
- **Library:** Serilog
- **Configuration:**
  - **Level:** User-configurable (Debug/Info/Warning/Error). Default: Information.
  - **Output:** Rolling file strategy (e.g., `logs/rawr-.log`), kept for 7 days.
  - **Location:** Subdirectory in the platform-specific data folder (e.g., `%APPDATA%\Rawr\Logs`).
- **Context:** Logs should include the component source (e.g., `[CalendarSyncService]`) to trace silent failures in background tasks.

## 4. Implementation Details

### 4.1 AlertScheduler (Sleep/Wake Resilience)
The scheduler cannot rely solely on `Task.Delay`.
- **Primary Mechanism:** Calculate `delay = nextEvent - now`. `Task.Delay(delay)`.
- **Secondary Mechanism (Heartbeat):** A `PeriodicTimer` fires every 30 seconds.
  - Checks if `DateTime.Now` > `nextEventTime`.
  - Checks if a "Time Jump" occurred (e.g., `Now - LastCheckTime > 2 * Interval`).
  - If a jump is detected (Wake from sleep), re-evaluate the schedule immediately.
- **Missed Events:**
  - If `Now > EventTime + Config.MissedEventThresholdMinutes` (default 60), the event is **Discarded** (silent log).
  - If `Now <= EventTime + Threshold`, the event fires **Immediately** (catch-up).

### 4.2 Linux Strategy (Wayland First)
- **Visual Alerts:**
  - **Primary:** `org.freedesktop.Notifications` (DBus). This ensures integration with GNOME/KDE/Sway "Do Not Disturb" modes and prevents window positioning issues on Wayland.
  - **Fallback:** Custom `NotificationPopup` (only if X11 session detected or user forces "Custom Window" mode).
- **Fullscreen Detection:**
  - **X11:** `_NET_ACTIVE_WINDOW` atom checks.
  - **Wayland:** No standard protocol for "active window is fullscreen". Feature will be "Best Effort" or disabled on Wayland unless specific compositor protocols (like Hyprland IPC) are supported.

### 4.3 Windows TTS Strategy
- **Service:** `WindowsVoiceService`
- **Logic:**
  1. Attempt to initialize `Windows.Media.SpeechSynthesis` (WinRT).
  2. If successful, enumerate "Modern" voices.
  3. Also enumerate `System.Speech` (SAPI) "Legacy" voices.
  4. **Settings UI:** Allow user to select from a combined list (e.g., "[Modern] Microsoft Zira", "[Legacy] Microsoft Sam").
  5. **Playback:** Use the appropriate engine based on the selected voice ID.

## 5. Data Models (Updated)

### 5.1 Configuration (JSON)
```json
{
  "General": {
    "StartWithOS": true,
    "MissedEventThresholdMinutes": 60,
    "HeartbeatIntervalSeconds": 30
  },
  "Calendar": {
    "Sources": [
      {
        "Id": "guid-1",
        "Name": "Personal",
        "Uri": "https://calendar.google.com/...",
        "Type": "Remote", // Remote, Local
        "AuthType": "None", // None, Basic, Bearer
        "AuthToken": "", // Encrypted or stored securely if possible
        "Color": "#FF0000",
        "Enabled": true
      }
    ],
    "SyncIntervalMinutes": 15
  },
  "Voice": {
    "Engine": "Auto",  // Auto, WinRT, SAPI, Embedded
    "VoiceId": "Default",
    "Rate": 1.0,
    "Volume": 100
  },
  "Logging": {
    "Level": "Information",
    "RetentionDays": 7
  },
  "Linux": {
    "ForceCustomWindow": false, // If false, uses libnotify on Wayland
    "NotificationUrgency": "Critical"
  }
}
```

## 6. Testing Strategy
- **Sleep/Wake:**
  - **Unit Test:** `FakeTimeProvider`. Advance time by 2 hours instantly (simulating sleep). Assert `AlertScheduler` fires "Catch-up" logic or "Skip" logic correctly.
- **Linux:**
  - Verify `libnotify` calls using `dbus-monitor`.
  - Verify "Wayland" detection logic (check `WAYLAND_DISPLAY` env var).

## 7. Future Considerations
- **Plugin System:** For "Active Window" detection on specific Wayland compositors (Hyprland, Sway).
- **Mobile Companion:** Sync settings via QR code.

# Rawr - Design Document

## 1. Overview
`Rawr` is a cross-platform desktop application built with Avalonia UI. It resides primarily in the system tray and provides voice-synthesized alerts and popup notifications for:
1.  **Calendar Events:** Meetings, appointments, and reminders from various sources.
2.  **Interval Chimes:** Periodic alerts (e.g., hourly) to help users maintain awareness of time passing (useful for work/school blocks).

## 2. Technology Stack
- **Framework:** Avalonia UI (Cross-platform XAML)
- **Runtime:** .NET 10.0
- **DI Container:** `Microsoft.Extensions.DependencyInjection`
- **ICS Parsing:** `Ical.Net` (Standard library for iCalendar handling)
- **Storage:**
  - **Settings/Cache:** JSON (System.Text.Json)
  - **Secrets:** OS Native Credential Stores (Windows Credential Manager, macOS Keychain, Linux libsecret/Gnome Keyring).
- **Logging:** Serilog (Rolling file sink, configurable).
- **Concurrency:** `SemaphoreSlim` for safe repository access; `System.Threading.Channels` for notification queuing.
- **Resilience:** Polly (Retry policies, Circuit Breakers).
- **Voice Synthesis:** Native OS API Abstraction (`IVoiceService`)
  - **Windows:** Hybrid `Windows.Media.SpeechSynthesis` (WinRT) with `System.Speech` (SAPI) fallback.
  - **macOS:** `NSSpeechSynthesizer` (via interop)
  - **Linux:** `speech-dispatcher` or `espeak` via CLI/library.
- **Audio Playback:** Abstraction (`IAudioPlaybackService`)
  - **Purpose:** Decouples TTS generation from playback to allow specific output device selection (e.g., "Headphones" vs "Speakers").
  - **Implementations:** `NAudio` (Windows), `miniaudio` / `NetCoreAudio` (Cross-platform) or platform-specific APIs.

## 3. Architecture

### 3.1 Components
- **Main Application:** Handles lifecycle, single-instance check, and tray icon management.
  - **Tray Menu Actions:** `Dashboard` (Open UI), `Sync Now`, `Pause Alerts` (Toggle), `Settings`, `Exit`.
- **Core Services:**
  - `CalendarRepository`: **(Singleton)** Thread-safe store. Handles expansion of recurring events into concrete instances.
  - `IOsIntegrationService`: Abstraction for OS-specific tasks (Start on Boot, Fullscreen detection, File Paths).
  - `CalendarSyncService`: Background service that fetches `.ics` streams. Uses `ICredentialStore` to retrieve auth tokens.
  - `AlertScheduler`: **(Singleton)** Manages the "Next Alert" logic. Handles system sleep/wake resilience.
  - `TimeAwarenessService`: **(Singleton)** Manages periodic "chimes" or time announcements (e.g., hourly). Independent of calendar events.
  - `VoiceService`: Generates TTS audio streams.
  - `AudioPlaybackService`: Routes audio streams to the configured output device.
  - `SettingsManager`: Handles persistence of user configuration.
  - `NotificationQueue`: **(Singleton)** Manages the serialization of popup alerts.
- **UI Windows:**
  - `DashboardWindow`: Main interface showing upcoming events and status.
  - `SettingsWindow`: Tabbed interface for configuration.
  - `NotificationPopup`: Custom borderless window (Windows/macOS/X11).
  - *Linux Wayland Strategy:* Uses `libnotify` (DBus).

### 3.2 Data Flow
1. **Sync:** `CalendarSyncService` fetches data -> parses via `Ical.Net`.
   - **Expansion:** Recurring events (RRULE) are expanded into concrete instances for a configurable look-ahead window (Default: **48 hours**).
2. **Persistence:** `CalendarRepository` flushes changes to `events.json`.
3. **Scheduling:** `AlertScheduler` calculates time to next event.
4. **Trigger:** `AlertScheduler` wakes up -> pushes alert to `NotificationQueue`.
   - **Snooze:** Snooze state is **volatile** (in-memory only). If the app restarts, snoozed events are treated as if the snooze expired (or re-evaluated based on time).
5. **Resilience:**
   - **Sync Failure:** Uses exponential backoff (Polly).
   - **Feedback:** Sync failure > 3 times updates tray icon to "Warning".

### 3.3 Security & File System
#### File Paths
- **Windows:** `%APPDATA%\Rawr`
- **macOS:** `~/Library/Application Support/Rawr`
- **Linux:** `$XDG_CONFIG_HOME/rawr` (Config), `$XDG_DATA_HOME/rawr` (Data)

#### Credential Storage
Authentication tokens are **never** stored in `events.json` or `settings.json`.
- **Windows:** `Windows Credential Manager` (Generic Credentials).
- **macOS:** `Keychain Services`.
- **Linux:** `libsecret` (DBus Secret Service API) or `Gnome Keyring`.
- *Fallback:* If a secure store is unavailable, prompt user for session-only credentials (do not persist to disk).

#### Authentication Types
- **PrivateUrl:** **(Preferred)** Uses a "Secret Address" (e.g., Google/Outlook private iCal URL). No authentication headers required; the token is in the URL.
- **Basic:** Standard Username/Password. **Note:** Not supported by modern Google/Microsoft accounts (require OAuth). Only for generic CalDAV/HTTP auth.
- **OAuth2:** *Not currently implemented.* Future scope for direct API integration.

### 3.4 Diagnostics & Logging
- **Library:** Serilog
- **Privacy:**
  - **PII Redaction:** Event Titles, Descriptions, and Locations must be redacted or hashed in `Information` level logs.
  - **Debug Mode:** Full details allowed only when `Logging.Level` is set to `Debug`.
- **Configuration:** Rolling file strategy, kept for 7 days.

## 4. Implementation Details

### 4.1 AlertScheduler (Sleep/Wake Resilience)
The scheduler cannot rely solely on `Task.Delay`.
- **Primary Mechanism:** Calculate `delay = nextEvent - now`. `Task.Delay(delay)`.
- **Secondary Mechanism (Heartbeat):** A `PeriodicTimer` fires every 30 seconds.
- **Missed Events:**
  - If `Now > EventTime + Config.MissedEventThresholdMinutes` (default 60), the event is **Discarded** (silent log).
  - If `Now <= EventTime + Threshold`, the event fires **Immediately** (catch-up).

### 4.2 Linux Strategy (Compatibility First)
- **Visual Alerts:**
  - **Primary (X11):** Custom `NotificationPopup` window.
  - **Fallback (Wayland):** `libnotify` (org.freedesktop.Notifications).
- **Fullscreen Detection:**
  - **X11:** `_NET_ACTIVE_WINDOW` atom checks.
  - **Wayland:** "Best Effort" or disabled.

### 4.3 Windows TTS Strategy
- **Service:** `WindowsVoiceService`
- **Logic:**
  1. Attempt to initialize `Windows.Media.SpeechSynthesis` (WinRT).
  2. If successful, enumerate "Modern" voices.
  3. Also enumerate `System.Speech` (SAPI) "Legacy" voices.
  4. **Settings UI:** User selects Voice ID and Output Device.

## 5. Data Models (Updated)

### 5.1 Configuration (JSON)
```json
{
  "General": {
    "StartWithOS": true,
    "MissedEventThresholdMinutes": 60,
    "HeartbeatIntervalSeconds": 30
  },
  "TimeAwareness": {
    "Enabled": false,
    "IntervalMinutes": 60, // e.g., Chime every hour
    "Sound": "Chime" // or "Voice" to speak the time
  },
  "Calendar": {
    "LookAheadHours": 48,
    "Sources": [
      {
        "Id": "guid-1",
        "Name": "Personal",
        "Uri": "https://calendar.google.com/...",
        "Type": "Remote", 
        "AuthType": "PrivateUrl", // "PrivateUrl" (No Headers) or "Basic" (User/Pass in OS Store)
        "Color": "#FF0000",
        "Enabled": true
      }
    ],
    "SyncIntervalMinutes": 15
  },
  "Voice": {
    "Engine": "Auto",
    "VoiceId": "Default",
    "DeviceId": "Default", // or specific GUID/Name
    "Rate": 1.0,
    "Volume": 100
  },
  "Logging": {
    "Level": "Information",
    "RetentionDays": 7
  },
  "Linux": {
    "ForceCustomWindow": false,
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

## 8. Project Structure
Recommended folder layout to separate concerns, might need to rearrange from current structure.

```
/
├── Rawr.sln
├── src/
│   ├── Rawr.Core/             # Business Logic, Interfaces, Models (NetStandard 2.1 / .NET 10)
│   │   ├── Domain/
│   │   ├── Interfaces/
│   │   └── Services/
│   ├── Rawr.UI/               # Avalonia Application (The "Head")
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Assets/
│   │   └── Program.cs
│   └── Rawr.Infrastructure/   # Concrete Implementations (Audio, OS, Persistence)
│       ├── Audio/
│       ├── Platform/
│       └── Persistence/
└── tests/
    ├── Rawr.Core.Tests/
    └── Rawr.Infrastructure.Tests/
```

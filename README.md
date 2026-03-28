# Rawr

A cross-platform desktop tray application that provides voice-synthesized alerts and popup notifications for calendar events and periodic time chimes.

Rawr lives in your system tray, syncs your calendars via iCal/ICS URLs, and announces upcoming meetings so you never miss one.

## Features

- System tray with configurable menu (Dashboard, Sync Now, Snooze, Mute, Settings)
- Calendar sync from remote ICS/iCal URLs (private URL or Basic auth)
- Voice announcements for upcoming events
- Periodic time chimes (e.g. hourly)
- Sleep/wake resilient scheduling
- Credentials stored in OS native secret stores

## Platform Support

| Feature | Windows | macOS | Linux |
|---|---|---|---|
| Calendar sync | Yes | Yes | Yes |
| Voice (TTS) | Yes (SAPI) | Yes (`say`) | TODO |
| Audio playback | Yes (NAudio) | Yes (`afplay`) | TODO |
| System tray | Yes | Yes | Yes |
| Start on boot | Yes (Registry) | Yes (LaunchAgent) | TODO |
| Fullscreen detection | Yes | No | TODO |
| Credential storage | Yes (DPAPI) | No (plaintext) | TODO |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### macOS

Requires macOS with the `say` and `afplay` commands (included by default on all macOS versions). Voice synthesis uses the system `say` command and audio playback uses `afplay`. Calendar source URLs are stored in plaintext in the settings file (Keychain integration is not yet implemented).

### Linux

> **TODO:** Linux voice, audio, credential storage, and OS integration services are not yet implemented. See [docs/CrossPlatformTasks.md](docs/CrossPlatformTasks.md).

## Getting Started

```bash
# Clone
git clone <repo-url>
cd Rawr

# Restore dependencies
dotnet restore

# Run (Debug)
dotnet run --project src/Rawr.UI

# Build (Release)
dotnet build -c Release

# Run tests
dotnet test
```

## Configuration

Settings are stored in:
- **Windows:** `%APPDATA%\Rawr\settings.json`
- **macOS:** `~/Library/Application Support/Rawr/settings.json`
- **Linux:** `$XDG_CONFIG_HOME/rawr/settings.json`

Calendar sources, voice preferences, sync intervals, and logging levels are all configured through the in-app Settings window or by editing `settings.json` directly.

## Project Structure

```
src/
  Rawr.Core/          # Business logic, interfaces, models
  Rawr.Infrastructure/ # Platform implementations (audio, OS, persistence)
  Rawr.UI/            # Avalonia UI application (views, viewmodels, tray)
tests/
  Rawr.Core.Tests/
  Rawr.Infrastructure.Tests/
docs/
  Design.md           # Architecture and design decisions
  CrossPlatformTasks.md # Remaining work for macOS/Linux
```

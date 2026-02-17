# Cross-Platform Implementation Tasks

The application currently targets `.NET 10.0` and uses **Avalonia UI**, making it structurally compatible with macOS and Linux. However, core services rely on Windows-specific APIs (`System.Speech`, `User32.dll`, Registry). Non-Windows platforms currently use "Dummy" services that provide no functionality.

To make the application fully functional on macOS and Linux, the following implementations are required.

## 1. Audio Playback (`IAudioPlaybackService`)
**Current State:** Uses `NAudio` (Windows MME/WASAPI).
**Goal:** Play generated or pre-recorded audio streams on macOS/Linux.

### Approaches
1.  **Cross-Platform Library:**
    *   **LibVLCSharp:** Robust, handles many formats, but requires shipping VLC libs.
    *   **NetCoreAudio:** Wraps CLI tools internally.
2.  **Platform-Specific Wrappers (Recommended for lightweight):**
    *   **macOS:** Implement `MacOsAudioService` that wraps the `afplay` command-line tool or uses `AVFoundation` via bindings (e.g., MonoMac/Xamarin.Mac logic if available in .NET 10).
    *   **Linux:** Implement `LinuxAudioService` that wraps `aplay` (ALSA), `paplay` (PulseAudio), or `pw-play` (PipeWire).

## 2. Text-to-Speech (`IVoiceService`)
**Current State:** Uses `System.Speech.Synthesis` (SAPI).
**Goal:** Generate speech from text string.

### macOS Implementation (`MacOsVoiceService`)
*   **CLI Approach:** Invoke the native `say` command.
    *   `Process.Start("say", "-v "VoiceName" "Text to speak"");`
    *   Supports `System.IO.Stream` output via `-o` flag (e.g., `say -o output.aiff ...`).
*   **Native API:** Use `AVSpeechSynthesizer` via generic bindings if accessible.

### Linux Implementation (`LinuxVoiceService`)
*   **CLI Approach:** Use `espeak-ng` or `festival`.
    *   `espeak-ng -w output.wav "Text"`
*   **Speech Dispatcher:** Use `spd-say` which abstracts the underlying engine.

## 3. OS Integration (`IOsIntegrationService`)
**Current State:** Uses Windows Registry for startup and `User32.dll` for fullscreen detection.

### Start on Boot
*   **macOS:** Create a Launch Agent `.plist` file in `~/Library/LaunchAgents/`.
    *   Key: `RunAtLoad` = `true`.
*   **Linux:** Create a `.desktop` entry in `~/.config/autostart/`.
    *   Format: Standard Freedesktop.org entry.

### Fullscreen / DND Detection
*   **macOS:**
    *   Check for "Do Not Disturb" or "Focus" mode status (requires specific macOS APIs/defaults read).
    *   Window bounds check typically requires Accessibility permissions.
*   **Linux:**
    *   **X11:** Use `xprop -root _NET_ACTIVE_WINDOW` to find active window and check `_NET_WM_STATE_FULLSCREEN`.
    *   **Wayland:** Difficult due to security isolation; usually requires specific compositor protocols or simply skipping this feature.

## 4. Implementation Plan
1.  **Refactor:** Ensure `IOsIntegrationService` and `IVoiceService` interfaces are generic enough to handle CLI-based async processes (e.g., `SynthesizeAsync` returning a Stream is good).
2.  **Implement:** Create `Rawr.Infrastructure.Mac` and `Rawr.Infrastructure.Linux` (or just fold into `Infrastructure` with conditional compilation/runtime checks).
3.  **Register:** Update `App.axaml.cs` to detect OS and register the correct service implementation.
    ```csharp
    if (OperatingSystem.IsWindows()) { ... }
    else if (OperatingSystem.IsMacOS()) { ... }
    else if (OperatingSystem.IsLinux()) { ... }
    ```

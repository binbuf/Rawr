# Task 6: Audio & Voice Infrastructure

## Goal
Implement Text-to-Speech (TTS) and Audio Playback services.

## Steps
1.  **Interfaces** (`Rawr.Core`):
    *   `IVoiceService`: `Task<Stream> SynthesizeAsync(string text, VoiceOptions options)`.
    *   `IAudioPlaybackService`: `Task PlayAsync(Stream audioStream, string deviceId)`.

2.  **Windows Implementation** (`Rawr.Infrastructure`):
    *   **Voice**: Implement `WindowsVoiceService`.
        *   Try `Windows.Media.SpeechSynthesis` (WinRT) first.
        *   Fallback to `System.Speech` if needed (or stick to one for MVP).
    *   **Playback**: Implement `NAudioPlaybackService` (using NAudio package).
        *   Allow selecting output device (by ID or name).

3.  **Cross-Platform Stub**:
    *   Create dummy implementations for Mac/Linux if not implementing immediately, or log "Not implemented".

4.  **Integration**:
    *   Register services in DI.

## Testing
*   **Manual**: Trigger a test sound via a temporary CLI command or Debug call.
*   **Unit**: Hard to unit test audio, but can mock the `IVoiceService` to verify it receives correct text.

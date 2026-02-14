# Task 2: Configuration, Logging & DI Setup

## Goal
Establish the foundational services for configuration management, logging, and dependency injection.

## Steps
1.  **Define Configuration Models** (`Rawr.Core`):
    *   Create the model classes matching the JSON structure in the Design Doc (Section 5.1).
    *   `RawrConfig`, `GeneralConfig`, `TimeAwarenessConfig`, `CalendarConfig`, `VoiceConfig`, `LoggingConfig`.
    *   Ensure defaults are set (e.g., `MissedEventThresholdMinutes = 60`).

2.  **Implement SettingsManager** (`Rawr.Infrastructure` or `Rawr.Core`):
    *   Create `ISettingsManager` interface.
    *   Implement `SettingsManager` which loads/saves `config.json` (or `settings.json`) from the OS-specific Application Data folder.
    *   Use `System.Environment.GetFolderPath` to determine the correct path based on OS.

3.  **Setup Logging** (`Rawr.UI` / `Rawr.Infrastructure`):
    *   Install `Serilog`, `Serilog.Sinks.File`, `Serilog.Sinks.Console`.
    *   Configure Serilog in the Program startup.
    *   Implement logic to respect `Logging.Level` from configuration.
    *   Ensure PII (Event Titles) can be redacted in logs (create a helper or custom formatter if needed, or just note it for later).

4.  **DI Container Setup** (`Rawr.UI`):
    *   In `App.axaml.cs` or `Program.cs`, set up `Microsoft.Extensions.DependencyInjection`.
    *   Register `ISettingsManager` as Singleton.
    *   Register `ILogger` (Serilog).

## Testing
*   **Unit Tests**:
    *   Test `SettingsManager` reading/writing to a temporary file.
    *   Verify default values are populated when file doesn't exist.
*   **Manual Verification**:
    *   Run the app, check that a log file is created in the expected folder.
    *   Check that a config file is created.

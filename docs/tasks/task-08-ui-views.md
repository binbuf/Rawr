# Task 8: UI - Dashboard & Settings Views

## Goal
Build the visual interface for users to see events and configure the app.

## Steps
1.  **Dashboard View**:
    *   [x] Display list of "Upcoming Events".
    *   [x] Show status of Sync (Last run, success/fail).
    *   [x] "Snooze/Dismiss" buttons for active alerts (if any).

2.  **Settings View**:
    *   [x] Tabbed interface: `General`, `Calendar`, `Voice`.
    *   [x] **Calendar**: List sources, Add/Remove/Edit Source (URL, Color).
    *   [x] **Voice**: Dropdown for Voice selection, Slider for Volume/Rate. Test button for TTS.
    *   [x] Bind these views to `SettingsViewModel` which interacts with `SettingsManager`.

3.  **Validation**:
    *   [x] Ensure settings are saved immediately or on "Apply".

## Testing
*   **Manual**: Verify binding works (changing setting updates JSON file).
*   **Build**: Project builds successfully.

# Task 8: UI - Dashboard & Settings Views

## Goal
Build the visual interface for users to see events and configure the app.

## Steps
1.  **Dashboard View**:
    *   Display list of "Upcoming Events".
    *   Show status of Sync (Last run, success/fail).
    *   "Snooze/Dismiss" buttons for active alerts (if any).

2.  **Settings View**:
    *   Tabbed interface: `General`, `Calendar`, `Voice`.
    *   **Calendar**: List sources, Add/Remove/Edit Source (URL, Color).
    *   **Voice**: Dropdown for Voice selection, Slider for Volume/Rate. Test button for TTS.
    *   Bind these views to `SettingsViewModel` which interacts with `SettingsManager`.

3.  **Validation**:
    *   Ensure settings are saved immediately or on "Apply".

## Testing
*   **Manual**: Verify binding works (changing setting updates JSON file).

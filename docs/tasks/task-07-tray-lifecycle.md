# Task 7: UI - Tray Icon & Application Lifecycle

## Goal
Make the application live in the system tray and handle background operation.

## Steps
1.  **Tray Icon** (`Rawr.UI`):
    *   Configure `TrayIcon` in `App.axaml`.
    *   Add Menu Items: `Dashboard`, `Sync Now`, `Settings`, `Exit`.
    *   **Icons**: Add a basic `.ico` resource.

2.  **Lifecycle Management**:
    *   Ensure closing the Main Window does *not* exit the app (Minimize to Tray).
    *   Ensure "Exit" from Tray actually kills the process.
    *   Implement **Single Instance** check (using `Mutex` or similar) to prevent multiple Rawr instances.

3.  **Wire up Menu Actions**:
    *   `Sync Now` -> Calls `CalendarSyncService.Sync()`.
    *   `Dashboard` -> Shows Main Window.

## Testing
*   **Manual**: Run app. Close window. Check tray. Click tray menu. Try to run second instance.

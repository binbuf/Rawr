# Task 9: Notifications & Time Awareness

## Goal
Implement the actual user-facing alerts (Popups) and the periodic Chime.

## Steps
1.  **Notification Popup**:
    *   Create a custom borderless `Window` in Avalonia.
    *   Style it to look like a toast notification.
    *   Positioning logic: Bottom Right (Windows default) or Top Right.
    *   **Queue**: Implement `NotificationQueue` service to show popups sequentially if multiple fire.

2.  **Time Awareness Service**:
    *   Implement `TimeAwarenessService`.
    *   Timer that fires on the hour (or configured interval).
    *   Uses `IVoiceService` to speak the time ("It is 2 PM") or play a chime sound.

3.  **Connect Scheduler to Notification**:
    *   Update `AlertScheduler` to push to `NotificationQueue` and call `IVoiceService` when an event triggers.

## Testing
*   **Manual**: Set an event 1 minute out. Wait. Verify Popup appears and Audio plays.
*   **Manual**: Enable Time Awareness. Wait for top of hour (or hack interval to 1 min). Verify chime.

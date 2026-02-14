# Task 5: Alert Scheduler Logic

## Goal
The core "brain" of the app. It decides when to trigger an alert.

## Steps
1.  **Scheduler Service** (`Rawr.Core`):
    *   Create `AlertScheduler` class.
    *   Dependencies: `ICalendarRepository`, `TimeProvider` (System.TimeProvider for testability).
    *   Implement `Start()` and `Stop()` methods.

2.  **Scheduling Logic**:
    *   Calculate `delay = nextEvent.Start - now`.
    *   Logic for "Missed Events":
        *   If `Now > EventTime + Threshold`: Log warning, discard.
        *   If `Now <= EventTime + Threshold`: Fire immediately (Catch-up).
    *   **Heartbeat**: Implement a `PeriodicTimer` (e.g., every 30s) to re-evaluate the next event (handles system sleep/wake drift).

3.  **Event Triggering**:
    *   Define an event or callback `OnAlertTriggered(Event evt)`.
    *   For now, just log that an alert was triggered.

## Testing
*   **Unit Tests (Crucial)**:
    *   Use `FakeTimeProvider`.
    *   **Scenario 1**: Event is 10 mins in future. Advance time 10 mins. Verify trigger.
    *   **Scenario 2 (Sleep)**: Event is 10 mins in future. Advance time 2 hours instantly. Verify "Missed" or "Catch-up" logic depending on threshold.
    *   **Scenario 3**: No events. Verify scheduler waits or sleeps efficiently.

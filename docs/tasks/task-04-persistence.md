# Task 4: Calendar Repository & Persistence

## Goal
Create a thread-safe store for events and persist them to disk so the app remembers state between restarts.

## Steps
1.  **Repository Interface** (`Rawr.Core`):
    *   `ICalendarRepository`: Methods for `SaveEvents(sourceId, events)`, `GetAllEvents()`, `GetNextEvent(afterTime)`.

2.  **Implementation** (`Rawr.Infrastructure`):
    *   Implement `CalendarRepository`.
    *   **Concurrency**: Use `SemaphoreSlim` to ensure thread safety when reading/writing the internal cache or file.
    *   **Persistence**: Serialize the aggregated list of events to `events.json` (or per-source files) in the data directory.

3.  **Integration with Sync**:
    *   Update `CalendarSyncService` (or a higher level orchestrator) to call `Repository.SaveEvents` after fetching.

## Testing
*   **Unit Tests**:
    *   Test concurrency: Simulate multiple threads trying to save events simultaneously.
    *   Test persistence: Save events, re-instantiate repository, ensure events are loaded back.

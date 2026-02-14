# Task 3: Calendar Domain & Sync Service

## Goal
Implement the ability to fetch and parse iCalendar (.ics) feeds.

## Steps
1.  **Domain Models** (`Rawr.Core`):
    *   Define `Event` model (Title, Start, End, Location, IsAllDay, SourceId).
    *   Define `CalendarSource` model (from Config).

2.  **ICS Parsing** (`Rawr.Infrastructure`):
    *   Add `Ical.Net` package to Infrastructure.
    *   Create `ICalendarParser` interface and implementation.
    *   Implement method to convert `Ical.Net.Calendar` objects into our domain `Event` objects.
    *   **Crucial**: Handle recurrence expansion. Use `Ical.Net`'s occurrence calculation to expand events for the next `N` hours (defined in Config, default 48).

3.  **Sync Service** (`Rawr.Infrastructure`):
    *   Create `ICalendarSyncService`.
    *   **State Notification**: Expose an event `event Action<bool> IsSyncingChanged` or standard `INotifyPropertyChanged` so the UI (Tray Icon) knows when to start/stop the animation.
    *   Implement logic to fetch `.ics` content from HTTP/HTTPS URLs (using `HttpClient`).
    *   Handle "PrivateUrl" auth (token in URL) and "Basic" auth (if needed later, scaffolding for now).
    *   Use `Polly` for retry logic on HTTP requests (Exponential backoff).

## Testing
*   **Unit Tests**:
    *   Create a sample `.ics` file string with recurring events.
    *   Test `CalendarParser` ensures recurring events are expanded correctly into individual `Event` instances.
    *   Test `CalendarParser` handles empty feeds gracefully.

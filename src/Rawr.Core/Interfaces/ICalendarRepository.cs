using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rawr.Core.Models;

namespace Rawr.Core.Interfaces;

public interface ICalendarRepository
{
    /// <summary>
    /// Saves a collection of events for a specific source.
    /// This should overwrite existing events for that source.
    /// </summary>
    Task SaveEventsAsync(string sourceId, IEnumerable<CalendarEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events from all sources.
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetAllEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the next upcoming event after the specified time.
    /// </summary>
    Task<CalendarEvent?> GetNextEventAsync(DateTimeOffset afterTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all stored calendar events.
    /// </summary>
    Task ClearAllEventsAsync(CancellationToken cancellationToken = default);
}

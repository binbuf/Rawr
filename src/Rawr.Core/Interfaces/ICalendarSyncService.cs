using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rawr.Core.Models;

namespace Rawr.Core.Interfaces;

public interface ICalendarSyncService
{
    Task<List<CalendarEvent>> SyncAsync(CancellationToken cancellationToken = default);
    event Action<bool>? IsSyncingChanged;
}

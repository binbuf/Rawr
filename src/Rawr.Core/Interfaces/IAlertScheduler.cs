using System;
using System.Threading;
using System.Threading.Tasks;
using Rawr.Core.Models;

namespace Rawr.Core.Interfaces;

public interface IAlertScheduler
{
    /// <summary>
    /// Starts the scheduling loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the scheduling loop.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Event triggered when an alert should be shown.
    /// </summary>
    event EventHandler<CalendarEvent> AlertTriggered;

    /// <summary>
    /// Gets or sets the time until which alerts are snoozed.
    /// </summary>
    DateTimeOffset? SnoozeUntil { get; set; }

    /// <summary>
    /// Manually triggers an alert for the specified event.
    /// </summary>
    void TriggerAlertManual(CalendarEvent evt);
}

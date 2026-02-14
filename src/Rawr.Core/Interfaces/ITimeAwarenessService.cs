using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rawr.Core.Interfaces;

public interface ITimeAwarenessService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Manually triggers a time announcement.
    /// </summary>
    /// <param name="simulatedTime">Optional time to simulate. If null, uses current time.</param>
    Task TriggerTimeAnnouncementManual(DateTimeOffset? simulatedTime = null);
}

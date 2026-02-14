using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rawr.Core.Interfaces;

public interface ITimeAwarenessService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

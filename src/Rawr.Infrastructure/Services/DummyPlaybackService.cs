using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;

namespace Rawr.Infrastructure.Services;

public class DummyPlaybackService : IAudioPlaybackService
{
    private readonly ILogger<DummyPlaybackService> _logger;

    public DummyPlaybackService(ILogger<DummyPlaybackService> logger)
    {
        _logger = logger;
    }

    public Task PlayAsync(Stream audioStream, string? deviceId = null)
    {
        _logger.LogInformation("DummyPlaybackService: Playing audio (Simulated). Device: {DeviceId}", deviceId ?? "Default");
        return Task.CompletedTask;
    }
}

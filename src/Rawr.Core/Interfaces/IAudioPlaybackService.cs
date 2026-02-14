namespace Rawr.Core.Interfaces;

public interface IAudioPlaybackService
{
    Task PlayAsync(Stream audioStream, string? deviceId = null);
}

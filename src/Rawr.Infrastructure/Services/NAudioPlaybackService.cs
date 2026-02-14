using NAudio.Wave;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;

namespace Rawr.Infrastructure.Services;

public class NAudioPlaybackService : IAudioPlaybackService
{
    private readonly ILogger<NAudioPlaybackService> _logger;

    public NAudioPlaybackService(ILogger<NAudioPlaybackService> logger)
    {
        _logger = logger;
    }

    public async Task PlayAsync(Stream audioStream, string? deviceId = null)
    {
        if (audioStream == null || audioStream.Length == 0)
        {
            _logger.LogWarning("PlayAsync called with empty or null stream.");
            return;
        }

        try
        {
            // Reset stream if needed
            if (audioStream.Position != 0 && audioStream.CanSeek)
            {
                audioStream.Position = 0;
            }

            await Task.Run(() =>
            {
                using var waveOut = new WaveOutEvent();
                
                // Select device if specified
                if (!string.IsNullOrEmpty(deviceId) && int.TryParse(deviceId, out int deviceNumber))
                {
                    waveOut.DeviceNumber = deviceNumber;
                }

                using var reader = new WaveFileReader(audioStream);
                waveOut.Init(reader);
                waveOut.Play();

                // Wait for playback to finish
                while (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing audio stream.");
        }
    }
}

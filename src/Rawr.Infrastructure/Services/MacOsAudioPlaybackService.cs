using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;

namespace Rawr.Infrastructure.Services;

[SupportedOSPlatform("macos")]
public class MacOsAudioPlaybackService : IAudioPlaybackService
{
    private readonly ILogger<MacOsAudioPlaybackService> _logger;
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    public MacOsAudioPlaybackService(ILogger<MacOsAudioPlaybackService> logger)
    {
        _logger = logger;
    }

    public async Task PlayAsync(Stream audioStream, string? deviceId = null)
    {
        if (audioStream == null || (audioStream.CanSeek && audioStream.Length == 0))
            return;

        var tempFile = Path.Combine(Path.GetTempPath(), $"rawr-play-{Guid.NewGuid()}.aiff");

        try
        {
            // Write stream to temp file
            await using (var fileStream = File.Create(tempFile))
            {
                await audioStream.CopyToAsync(fileStream);
            }

            // Verify the file has content
            if (new FileInfo(tempFile).Length == 0)
            {
                _logger.LogWarning("Audio stream was empty, skipping playback");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "afplay",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(tempFile);

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("Failed to start 'afplay' process");
                return;
            }

            using var cts = new CancellationTokenSource(ProcessTimeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("'afplay' process timed out after {Timeout}s", ProcessTimeout.TotalSeconds);
                process.Kill(entireProcessTree: true);
                return;
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogError("'afplay' exited with code {Code}: {Error}", process.ExitCode, stderr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing audio via 'afplay'");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }
}

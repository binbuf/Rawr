using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

[SupportedOSPlatform("macos")]
public class MacOsVoiceService : IVoiceService
{
    private readonly ILogger<MacOsVoiceService> _logger;
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    public MacOsVoiceService(ILogger<MacOsVoiceService> logger)
    {
        _logger = logger;
    }

    public async Task<Stream> SynthesizeAsync(string text, VoiceOptions options)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"rawr-tts-{Guid.NewGuid()}.aiff");

        try
        {
            var args = new List<string>();

            if (!string.IsNullOrEmpty(options.VoiceId) && options.VoiceId != "Default")
            {
                args.Add("-v");
                args.Add(options.VoiceId);
            }

            // Map Rate multiplier (0.5-2.0, default 1.0) to say WPM (default ~175)
            var wpm = (int)(175 * options.Rate);
            args.Add("-r");
            args.Add(wpm.ToString());

            args.Add("-o");
            args.Add(tempFile);

            args.Add("--");
            args.Add(text);

            var psi = new ProcessStartInfo
            {
                FileName = "say",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("Failed to start 'say' process");
                return new MemoryStream();
            }

            using var cts = new CancellationTokenSource(ProcessTimeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("'say' process timed out after {Timeout}s", ProcessTimeout.TotalSeconds);
                process.Kill(entireProcessTree: true);
                return new MemoryStream();
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogError("'say' exited with code {Code}: {Error}", process.ExitCode, stderr);
                return new MemoryStream();
            }

            // Read the AIFF file into a MemoryStream
            var memoryStream = new MemoryStream();
            await using (var fileStream = File.OpenRead(tempFile))
            {
                await fileStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech via 'say'");
            return new MemoryStream();
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    public IEnumerable<VoiceInfo> GetInstalledVoices()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "say",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("?");

            using var process = Process.Start(psi);
            if (process == null)
                return [new VoiceInfo { Id = "Default", Name = "Default Voice", Culture = "en-US", Gender = "Neutral" }];

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var voices = new List<VoiceInfo>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // Format: "Name                 locale  # description"
                var hashIndex = line.IndexOf('#');
                var mainPart = hashIndex >= 0 ? line[..hashIndex].TrimEnd() : line.TrimEnd();

                // Split on multiple spaces to separate name from locale
                var parts = mainPart.Split("  ", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                var name = parts[0].Trim();
                var locale = parts[1].Trim();

                voices.Add(new VoiceInfo
                {
                    Id = name,
                    Name = name,
                    Culture = locale.Replace('_', '-'),
                    Gender = "Neutral"
                });
            }

            return voices.Count > 0
                ? voices
                : [new VoiceInfo { Id = "Default", Name = "Default Voice", Culture = "en-US", Gender = "Neutral" }];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting installed voices via 'say'");
            return [new VoiceInfo { Id = "Default", Name = "Default Voice", Culture = "en-US", Gender = "Neutral" }];
        }
    }
}

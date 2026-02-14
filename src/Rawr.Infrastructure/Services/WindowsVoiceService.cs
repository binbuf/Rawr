using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

public class WindowsVoiceService : IVoiceService
{
    private readonly ILogger<WindowsVoiceService> _logger;

    public WindowsVoiceService(ILogger<WindowsVoiceService> logger)
    {
        _logger = logger;
    }

    public Task<Stream> SynthesizeAsync(string text, VoiceOptions options)
    {
        return Task.Run(() =>
        {
            var memoryStream = new MemoryStream();
            
            // We verify platform compatibility at runtime to be safe, 
            // though this service should likely only be registered on Windows.
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("WindowsVoiceService called on non-Windows platform.");
                return (Stream)memoryStream;
            }

            try
            {
                using var synth = new SpeechSynthesizer();
                
                // Configure Voice
                if (!string.IsNullOrEmpty(options.VoiceId) && options.VoiceId != "Default")
                {
                    try
                    {
                        synth.SelectVoice(options.VoiceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to select voice '{VoiceId}'. Falling back to default.", options.VoiceId);
                    }
                }

                // Configure Rate (-10 to 10 for System.Speech)
                // Mapping generic 0.5x - 2.0x to System.Speech range approximately
                int rate = 0;
                if (options.Rate > 1.0) rate = (int)((options.Rate - 1.0) * 10);
                else if (options.Rate < 1.0) rate = (int)((options.Rate - 1.0) * 10);
                synth.Rate = Math.Clamp(rate, -10, 10);

                // Configure Volume (0-100)
                synth.Volume = Math.Clamp(options.Volume, 0, 100);

                synth.SetOutputToWaveStream(memoryStream);
                synth.Speak(text);
                
                // Reset position so consumer can read from the beginning
                memoryStream.Position = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synthesizing speech.");
                // Return empty stream or rethrow? 
                // Returning empty stream to avoid crashing consumer
            }

            return (Stream)memoryStream;
        });
    }
}

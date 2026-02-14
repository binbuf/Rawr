using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

public class DummyVoiceService : IVoiceService
{
    private readonly ILogger<DummyVoiceService> _logger;

    public DummyVoiceService(ILogger<DummyVoiceService> logger)
    {
        _logger = logger;
    }

    public Task<Stream> SynthesizeAsync(string text, VoiceOptions options)
    {
        _logger.LogInformation("DummyVoiceService: Synthesizing '{Text}' (Simulated)", text);
        // Return an empty stream
        return Task.FromResult<Stream>(new MemoryStream());
    }
}

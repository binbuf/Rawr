using Rawr.Core.Models;

namespace Rawr.Core.Interfaces;

public interface IVoiceService
{
    Task<Stream> SynthesizeAsync(string text, VoiceOptions options);
}

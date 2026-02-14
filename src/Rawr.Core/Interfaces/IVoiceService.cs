using Rawr.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Rawr.Core.Interfaces;

public interface IVoiceService
{
    Task<Stream> SynthesizeAsync(string text, VoiceOptions options);
    IEnumerable<VoiceInfo> GetInstalledVoices();
}

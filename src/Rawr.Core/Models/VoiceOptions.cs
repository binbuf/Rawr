namespace Rawr.Core.Models;

public class VoiceOptions
{
    public string? VoiceId { get; set; }
    public double Rate { get; set; } = 1.0;
    public int Volume { get; set; } = 100;
}

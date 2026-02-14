using Rawr.Core.Configuration;

namespace Rawr.Core.Interfaces;

public interface ISettingsManager
{
    RawrConfig Settings { get; }
    void Save();
    void Load();
}

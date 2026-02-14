using Rawr.Core.Interfaces;

namespace Rawr.Infrastructure.Services;

public class DummyOsIntegrationService : IOsIntegrationService
{
    public void SetStartWithOs(bool enable) { }

    public bool IsFullscreen() => false;
}

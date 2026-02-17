using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

public class DummyOsIntegrationService : IOsIntegrationService
{
    public void SetStartWithOs(bool enable) { }

    public bool IsFullscreen() => false;

    public FocusAssistState GetFocusAssistState() => FocusAssistState.Off;
}

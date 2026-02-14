namespace Rawr.Core.Interfaces;

public interface IOsIntegrationService
{
    /// <summary>
    /// Enables or disables starting the application with the OS.
    /// </summary>
    void SetStartWithOs(bool enable);

    /// <summary>
    /// Returns true if a fullscreen application is currently active.
    /// </summary>
    bool IsFullscreen();
}

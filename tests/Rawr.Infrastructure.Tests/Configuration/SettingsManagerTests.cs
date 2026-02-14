using System;
using System.IO;
using Rawr.Infrastructure.Configuration;
using Xunit;

namespace Rawr.Infrastructure.Tests.Configuration;

public class SettingsManagerTests : IDisposable
{
    private readonly string _testBasePath;

    public SettingsManagerTests()
    {
        // Use a unique temp path for each test
        _testBasePath = Path.Combine(Path.GetTempPath(), "RawrTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testBasePath);
    }

    [Fact]
    public void Constructor_CreatesDefaultSettingsFile_WhenNoneExists()
    {
        // Arrange & Act
        var manager = new SettingsManager(_testBasePath);
        var expectedPath = Path.Combine(_testBasePath, "Rawr", "settings.json");

        // Assert
        Assert.NotNull(manager.Settings);
        Assert.True(File.Exists(expectedPath), $"File should exist at {expectedPath}");
        Assert.Equal(60, manager.Settings.General.MissedEventThresholdMinutes); // Default
    }

    [Fact]
    public void Save_PersistsChanges_ToDisk()
    {
        // Arrange
        var manager = new SettingsManager(_testBasePath);
        manager.Settings.General.MissedEventThresholdMinutes = 120;
        manager.Settings.Logging.Level = "Debug";

        // Act
        manager.Save();

        // Assert
        // Create a fresh manager pointing to the same path to verify reload
        var newManager = new SettingsManager(_testBasePath);
        Assert.Equal(120, newManager.Settings.General.MissedEventThresholdMinutes);
        Assert.Equal("Debug", newManager.Settings.Logging.Level);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBasePath))
            {
                Directory.Delete(_testBasePath, true);
            }
        }
        catch 
        {
            // Best effort cleanup
        }
    }
}

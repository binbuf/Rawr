using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using Rawr.Core.Services;
using Xunit;

namespace Rawr.Core.Tests.Services;

public class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    
    // Callback to let test know we are waiting
    public Action<TimeSpan>? OnDelay { get; set; }

    private TimerCallback? _activeCallback;
    private object? _activeState;

    public ManualTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        OnDelay?.Invoke(dueTime);
        
        _activeCallback = callback;
        _activeState = state;
        
        return new Mock<ITimer>().Object;
    }

    public void Advance(TimeSpan amount)
    {
        _now += amount;
    }

    public void CompleteDelay()
    {
        // Invoke the callback that the scheduler provided to the timer
        _activeCallback?.Invoke(_activeState);
        // Do not clear it immediately if we assume period? But period is Zero in WaitAsync.
        // So one-shot.
        _activeCallback = null;
        _activeState = null;
    }
}

public class AlertSchedulerTests
{
    private readonly Mock<ICalendarRepository> _repoMock;
    private readonly Mock<ISettingsManager> _settingsMock;
    private readonly Mock<ILogger<AlertScheduler>> _loggerMock;
    private readonly ManualTimeProvider _timeProvider;
    private readonly RawrConfig _config;

    public AlertSchedulerTests()
    {
        _repoMock = new Mock<ICalendarRepository>();
        _settingsMock = new Mock<ISettingsManager>();
        _loggerMock = new Mock<ILogger<AlertScheduler>>();
        
        var startTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _timeProvider = new ManualTimeProvider(startTime);

        _config = new RawrConfig();
        _config.General.HeartbeatIntervalSeconds = 3600; // 1 hour, to test event wait time logic
        _settingsMock.Setup(s => s.Settings).Returns(_config);
        _settingsMock.Setup(s => s.AppDataPath).Returns(Path.Combine(Path.GetTempPath(), "RawrTests_" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public async Task StartAsync_ShouldProcessImmediateEvents()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var evt = new CalendarEvent
        {
            Uid = "1",
            SourceId = "test",
            Title = "Immediate Event",
            Start = now.AddMinutes(-5), // 5 mins ago (within 60m threshold)
            End = now.AddMinutes(25)
        };

        _repoMock.Setup(r => r.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CalendarEvent> { evt });

        var alertFired = false;
        using var scheduler = new AlertScheduler(_repoMock.Object, _settingsMock.Object, _timeProvider, _loggerMock.Object);
        scheduler.AlertTriggered += (s, e) => alertFired = true;

        // Act
        var delayTcs = new TaskCompletionSource<bool>();
        _timeProvider.OnDelay = (d) => delayTcs.TrySetResult(true);

        await scheduler.StartAsync(CancellationToken.None);
        
        // Wait for loop to hit first delay (which happens AFTER processing immediate events)
        await Task.WhenAny(delayTcs.Task, Task.Delay(2000));

        // Assert
        Assert.True(alertFired, "Alert should have fired immediately for catch-up event.");
        
        // Cleanup
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldWaitForFutureEvent()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var evt = new CalendarEvent
        {
            Uid = "2",
            SourceId = "test",
            Title = "Future Event",
            Start = now.AddMinutes(10), 
            End = now.AddMinutes(40)
        };

        _repoMock.Setup(r => r.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CalendarEvent> { evt });

        var alertFired = false;
        using var scheduler = new AlertScheduler(_repoMock.Object, _settingsMock.Object, _timeProvider, _loggerMock.Object);
        scheduler.AlertTriggered += (s, e) => alertFired = true;

        TimeSpan? requestedDelay = null;
        var delayTcs = new TaskCompletionSource<bool>();
        _timeProvider.OnDelay = (d) => 
        {
            requestedDelay = d;
            delayTcs.TrySetResult(true);
        };

        // Act
        await scheduler.StartAsync(CancellationToken.None);
        
        // Wait for loop to hit delay
        await Task.WhenAny(delayTcs.Task, Task.Delay(2000));

        // Assert 1: Should wait approx 10 mins
        Assert.NotNull(requestedDelay);
        Assert.Equal(TimeSpan.FromMinutes(10), requestedDelay.Value);
        Assert.False(alertFired, "Should not fire yet.");

        // Act 2: Advance time and wake up
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        
        // Reset capture for next loop
        var nextDelayTcs = new TaskCompletionSource<bool>();
        _timeProvider.OnDelay = (d) => nextDelayTcs.TrySetResult(true);
        
        // Trigger timer callback
        _timeProvider.CompleteDelay();

        // Wait for next loop iteration
        await Task.WhenAny(nextDelayTcs.Task, Task.Delay(2000));

        // Assert 2: Should have fired
        Assert.True(alertFired, "Should fire after waking up.");

        // Cleanup
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldIgnoreOldEvents()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var evt = new CalendarEvent
        {
            Uid = "3",
            SourceId = "test",
            Title = "Ancient Event",
            Start = now.AddMinutes(-120), // 2 hours ago (outside 60m threshold)
            End = now.AddMinutes(-90)
        };

        _repoMock.Setup(r => r.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CalendarEvent> { evt });

        var alertFired = false;
        using var scheduler = new AlertScheduler(_repoMock.Object, _settingsMock.Object, _timeProvider, _loggerMock.Object);
        scheduler.AlertTriggered += (s, e) => alertFired = true;

        var delayTcs = new TaskCompletionSource<bool>();
        _timeProvider.OnDelay = (d) => delayTcs.TrySetResult(true);

        // Act
        await scheduler.StartAsync(CancellationToken.None);
        await Task.WhenAny(delayTcs.Task, Task.Delay(2000));

        // Assert
        Assert.False(alertFired, "Should not fire for old event.");
        
        // Cleanup
        await scheduler.StopAsync(CancellationToken.None);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using Rawr.Infrastructure.Persistence;
using Xunit;

namespace Rawr.Infrastructure.Tests.Persistence;

public class CalendarRepositoryTests : IDisposable
{
    private readonly string _tempPath;
    private readonly MockSettingsManager _settingsManager;
    private readonly CalendarRepository _repository;

    public CalendarRepositoryTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "RawrTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        _settingsManager = new MockSettingsManager(_tempPath);
        _repository = new CalendarRepository(_settingsManager, NullLogger<CalendarRepository>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, true); } catch { }
        }
    }

    [Fact]
    public async Task SaveEvents_PersistsToFile()
    {
        var sourceId = "test-source";
        var events = new List<CalendarEvent>
        {
            new CalendarEvent { Uid = "1", Title = "Event 1", Start = DateTimeOffset.Now, SourceId = sourceId }
        };

        await _repository.SaveEventsAsync(sourceId, events);

        var filePath = Path.Combine(_tempPath, "events.json");
        Assert.True(File.Exists(filePath));
        
        var json = await File.ReadAllTextAsync(filePath);
        Assert.Contains("Event 1", json);
    }

    [Fact]
    public async Task GetAllEvents_RetrievesSavedEvents()
    {
        var sourceId = "test-source";
        var events = new List<CalendarEvent>
        {
            new CalendarEvent { Uid = "1", Title = "Event 1", Start = DateTimeOffset.Now, SourceId = sourceId }
        };

        await _repository.SaveEventsAsync(sourceId, events);

        // Create a new repository instance to simulate app restart
        var newRepo = new CalendarRepository(_settingsManager, NullLogger<CalendarRepository>.Instance);
        var loadedEvents = await newRepo.GetAllEventsAsync();

        Assert.Single(loadedEvents);
        Assert.Equal("Event 1", loadedEvents.First().Title);
    }

    [Fact]
    public async Task SaveEvents_OverwritesSourceEvents()
    {
        var sourceId = "test-source";
        var initialEvents = new List<CalendarEvent>
        {
            new CalendarEvent { Uid = "1", Title = "Event 1", Start = DateTimeOffset.Now, SourceId = sourceId }
        };

        await _repository.SaveEventsAsync(sourceId, initialEvents);

        var updatedEvents = new List<CalendarEvent>
        {
            new CalendarEvent { Uid = "2", Title = "Event 2", Start = DateTimeOffset.Now, SourceId = sourceId }
        };

        await _repository.SaveEventsAsync(sourceId, updatedEvents);

        var loadedEvents = await _repository.GetAllEventsAsync();
        Assert.Single(loadedEvents);
        Assert.Equal("Event 2", loadedEvents.First().Title);
    }

    [Fact]
    public async Task GetNextEvent_ReturnsCorrectEvent()
    {
        var sourceId = "test-source";
        var now = DateTimeOffset.Now;
        var events = new List<CalendarEvent>
        {
            new CalendarEvent { Uid = "1", Title = "Past", Start = now.AddHours(-1), SourceId = sourceId },
            new CalendarEvent { Uid = "2", Title = "Next", Start = now.AddHours(1), SourceId = sourceId },
            new CalendarEvent { Uid = "3", Title = "Future", Start = now.AddHours(2), SourceId = sourceId }
        };

        await _repository.SaveEventsAsync(sourceId, events);

        var next = await _repository.GetNextEventAsync(now);
        Assert.NotNull(next);
        Assert.Equal("Next", next!.Title);
    }

    [Fact]
    public async Task Concurrency_SaveEvents_IsThreadSafe()
    {
        var tasks = new List<Task>();
        int threadCount = 10;
        
        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                var sourceId = $"source-{index}";
                var events = new List<CalendarEvent>
                {
                    new CalendarEvent { Uid = $"{index}", Title = $"Event {index}", Start = DateTimeOffset.Now, SourceId = sourceId }
                };
                await _repository.SaveEventsAsync(sourceId, events);
            }));
        }

        await Task.WhenAll(tasks);

        var allEvents = await _repository.GetAllEventsAsync();
        Assert.Equal(threadCount, allEvents.Count());
    }

    private class MockSettingsManager : ISettingsManager
    {
        public RawrConfig Settings { get; } = new();
        public string AppDataPath { get; }

        public MockSettingsManager(string path)
        {
            AppDataPath = path;
        }

        public void Save() { }
        public void Load() { }
    }
}

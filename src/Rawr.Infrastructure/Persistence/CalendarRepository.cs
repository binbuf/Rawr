using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Persistence;

public class CalendarRepository : ICalendarRepository
{
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<CalendarRepository> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _storagePath;
    
    // In-memory cache: SourceId -> List of Events
    private Dictionary<string, List<CalendarEvent>> _eventsBySource = new();
    private bool _isLoaded = false;

    public CalendarRepository(ISettingsManager settingsManager, ILogger<CalendarRepository> logger)
    {
        _settingsManager = settingsManager;
        _logger = logger;
        _storagePath = Path.Combine(_settingsManager.AppDataPath, "events.json");
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded) return;

        if (!File.Exists(_storagePath))
        {
            _isLoaded = true;
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_storagePath, cancellationToken);
            var allEvents = JsonSerializer.Deserialize<List<CalendarEvent>>(json) ?? new List<CalendarEvent>();
            
            // Rebuild the dictionary
            _eventsBySource = allEvents
                .GroupBy(e => e.SourceId)
                .ToDictionary(g => g.Key, g => g.ToList());
                
            _logger.LogInformation("Loaded {Count} events from storage.", allEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load events from storage at {Path}", _storagePath);
            // On error, start with empty to avoid crashing app loop, but data might be lost/stale.
            _eventsBySource = new Dictionary<string, List<CalendarEvent>>();
        }
        finally
        {
            _isLoaded = true;
        }
    }

    public async Task SaveEventsAsync(string sourceId, IEnumerable<CalendarEvent> events, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);

            // Update in-memory
            if (_eventsBySource.ContainsKey(sourceId))
            {
                _eventsBySource[sourceId] = events.ToList();
            }
            else
            {
                _eventsBySource.Add(sourceId, events.ToList());
            }

            // Persist to disk (Flatten)
            var allEvents = _eventsBySource.Values.SelectMany(x => x).ToList();
            var json = JsonSerializer.Serialize(allEvents, new JsonSerializerOptions { WriteIndented = true });
            
            // Ensure directory exists (SettingsManager usually handles creating the root, but good to be safe)
            var dir = Path.GetDirectoryName(_storagePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(_storagePath, json, cancellationToken);
            _logger.LogDebug("Saved {Count} events to storage.", allEvents.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<CalendarEvent>> GetAllEventsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _eventsBySource.Values.SelectMany(e => e).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<CalendarEvent?> GetNextEventAsync(DateTimeOffset afterTime, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);

            return _eventsBySource.Values
                .SelectMany(e => e)
                .Where(e => e.Start > afterTime)
                .OrderBy(e => e.Start)
                .FirstOrDefault();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAllEventsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _eventsBySource.Clear();
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
            _logger.LogInformation("All calendar events have been cleared.");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

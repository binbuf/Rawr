using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Rawr.Infrastructure.Services;

public class CalendarSyncService : ICalendarSyncService
{
    private readonly ISettingsManager _settingsManager;
    private readonly ICalendarParser _parser;
    private readonly ICalendarRepository _repository;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CalendarSyncService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly Dictionary<string, string> _sourceHashes = new();
    private readonly string _icsCacheDir;

    public event Action<bool>? IsSyncingChanged;

    public CalendarSyncService(
        ISettingsManager settingsManager,
        ICalendarParser parser,
        ICalendarRepository repository,
        HttpClient httpClient,
        ILogger<CalendarSyncService> logger)
    {
        _settingsManager = settingsManager;
        _parser = parser;
        _repository = repository;
        _httpClient = httpClient;
        _logger = logger;

        _icsCacheDir = Path.Combine(_settingsManager.AppDataPath, "ics-cache");
        Directory.CreateDirectory(_icsCacheDir);

        // Configure Polly
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning("Retry {AttemptNumber} for fetch. Error: {Error}", args.AttemptNumber, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<List<CalendarEvent>> SyncAsync(CancellationToken cancellationToken = default)
    {
        IsSyncingChanged?.Invoke(true);
        var allEvents = new List<CalendarEvent>();

        try
        {
            var sources = _settingsManager.Settings.Calendar.Sources;
            var lookAhead = _settingsManager.Settings.Calendar.LookAheadHours;

            foreach (var source in sources)
            {
                if (!source.Enabled) continue;

                try
                {
                    var (events, changed) = await SyncSourceAsync(source, lookAhead, cancellationToken);
                    if (!changed)
                    {
                        _logger.LogDebug("Source {SourceName} hash unchanged, skipping", source.Name);
                        continue;
                    }

                    var eventList = events as List<CalendarEvent> ?? new List<CalendarEvent>(events);
                    await _repository.SaveEventsAsync(source.Id, eventList, cancellationToken);
                    allEvents.AddRange(eventList);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync source {SourceName} ({SourceUri})", source.Name, source.Uri);
                }
            }
        }
        finally
        {
            // Reclaim LOH memory from Ical.Net Calendar object graphs.
            // Must be blocking + compacting to actually return LOH memory to the OS.
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            _logger.LogInformation("Post-sync GC: managed heap {HeapSize} MB",
                GC.GetTotalMemory(false) / (1024 * 1024));

            IsSyncingChanged?.Invoke(false);
        }

        return allEvents;
    }

    private async Task<(IEnumerable<CalendarEvent> Events, bool Changed)> SyncSourceAsync(CalendarSource source, int lookAhead, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Uri))
        {
            return (Array.Empty<CalendarEvent>(), false);
        }

        var cacheFile = Path.Combine(_icsCacheDir, $"{source.Id}.ics");

        // Stream HTTP response directly to disk — no MemoryStream, no LOH allocation
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            using var response = await _httpClient.GetAsync(source.Uri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(cacheFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            await response.Content.CopyToAsync(fs, ct);
        }, cancellationToken);

        // Compute SHA256 hash by streaming from disk (no byte array in memory)
        string hash;
        {
            using var hashStream = new FileStream(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            var hashBytes = await SHA256.HashDataAsync(hashStream, cancellationToken);
            hash = Convert.ToHexString(hashBytes);
        }

        if (_sourceHashes.TryGetValue(source.Id, out var previousHash) && previousHash == hash)
        {
            return (Array.Empty<CalendarEvent>(), false);
        }

        _sourceHashes[source.Id] = hash;

        // Parse from disk file — Ical.Net Calendar object is scoped to the Parse method
        // and becomes eligible for GC as soon as Parse returns the materialized event list
        List<CalendarEvent> events;
        {
            using var parseStream = new FileStream(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            events = new List<CalendarEvent>(_parser.Parse(parseStream, source, lookAhead));
        }

        _logger.LogInformation("Parsed {Count} events from source {SourceName} ({FileSize} bytes on disk)",
            events.Count, source.Name, new FileInfo(cacheFile).Length);

        return (events, true);
    }
}

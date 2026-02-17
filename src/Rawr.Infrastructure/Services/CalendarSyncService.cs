using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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

        // Use Polly to fetch
        var icsContent = await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            var response = await _httpClient.GetAsync(source.Uri, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }, cancellationToken);

        // SHA256 change detection
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(icsContent)));
        if (_sourceHashes.TryGetValue(source.Id, out var previousHash) && previousHash == hash)
        {
            return (Array.Empty<CalendarEvent>(), false);
        }

        _sourceHashes[source.Id] = hash;
        return (_parser.Parse(icsContent, source, lookAhead), true);
    }
}

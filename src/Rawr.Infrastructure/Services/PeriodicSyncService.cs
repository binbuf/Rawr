using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;

namespace Rawr.Infrastructure.Services;

public class PeriodicSyncService : IDisposable
{
    private readonly ICalendarSyncService _syncService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<PeriodicSyncService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _serviceTask;

    public PeriodicSyncService(
        ICalendarSyncService syncService,
        ISettingsManager settingsManager,
        ILogger<PeriodicSyncService> logger)
    {
        _syncService = syncService;
        _settingsManager = settingsManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_serviceTask != null && !_serviceTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serviceTask = RunServiceLoopAsync(_cts.Token);
        _logger.LogInformation("PeriodicSyncService started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_serviceTask != null)
            {
                try
                {
                    await _serviceTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            _cts.Dispose();
            _cts = null;
        }
        _logger.LogInformation("PeriodicSyncService stopped.");
    }

    private async Task RunServiceLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var settings = _settingsManager.Settings.Calendar;
                var interval = TimeSpan.FromMinutes(settings.SyncIntervalMinutes);
                if (interval <= TimeSpan.Zero) interval = TimeSpan.FromMinutes(15);

                try
                {
                    _logger.LogInformation("Starting periodic sync...");
                    await _syncService.SyncAsync(token);
                    _logger.LogInformation("Periodic sync completed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during periodic sync.");
                }

                await Task.Delay(interval, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PeriodicSyncService loop.");
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

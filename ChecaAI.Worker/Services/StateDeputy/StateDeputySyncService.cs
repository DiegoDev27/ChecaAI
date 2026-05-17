using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChecaAI.Worker.Configuration;

namespace ChecaAI.Worker.Services.StateDeputy;

public class StateDeputySyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StateDeputySyncService> _logger;
    private readonly DataSyncOptions _syncOptions;
    private Timer? _scheduledTimer;

    public StateDeputySyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<StateDeputySyncService> logger,
        IOptions<DataSyncOptions> syncOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _syncOptions = syncOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("State Deputy Sync Service started");

        if (!_syncOptions.EnableScheduledSync)
        {
            _logger.LogInformation("Scheduled sync is disabled. State deputy service will run once and exit.");
            await RunSingleSyncAsync();
            return;
        }

        var initialDelay = CalculateInitialDelay();
        _logger.LogInformation("State deputy scheduled sync will start in {Delay} and repeat every {Interval}",
            initialDelay, _syncOptions.StateDeputyDataSyncInterval);

        _scheduledTimer = new Timer(async _ => await ExecuteSyncWithRetry(),
            null, initialDelay, _syncOptions.StateDeputyDataSyncInterval);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("State Deputy Sync Service is shutting down");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("State Deputy Sync Service is stopping");
        _scheduledTimer?.Change(Timeout.Infinite, 0);
        _scheduledTimer?.Dispose();
        await base.StopAsync(cancellationToken);
    }

    private async Task RunSingleSyncAsync()
    {
        try
        {
            await ExecuteSyncWithRetry();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete single state deputy sync operation");
        }
    }

    private async Task ExecuteSyncWithRetry()
    {
        var attempt = 1;

        while (attempt <= _syncOptions.RetryAttempts)
        {
            try
            {
                _logger.LogInformation("Starting state deputy synchronization - Attempt {Attempt}/{MaxAttempts}",
                    attempt, _syncOptions.RetryAttempts);

                await ExecuteSyncAsync();

                _logger.LogInformation("State deputy synchronization completed successfully");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "State deputy synchronization failed on attempt {Attempt}/{MaxAttempts}",
                    attempt, _syncOptions.RetryAttempts);

                if (attempt == _syncOptions.RetryAttempts)
                {
                    _logger.LogError("All retry attempts exhausted. State deputy synchronization failed definitively");
                    return;
                }

                attempt++;
                await Task.Delay(_syncOptions.RetryDelay);
            }
        }
    }

    private async Task ExecuteSyncAsync()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scrappers = scope.ServiceProvider.GetRequiredService<IEnumerable<IStateDeputyScrapperService>>();
            var persistenceService = scope.ServiceProvider.GetRequiredService<IStateDeputyPersistenceService>();

            var totalProcessed = 0;

            foreach (var scrapper in scrappers)
            {
                try
                {
                    _logger.LogInformation("Processing {AssemblyName} ({State})...", scrapper.AssemblyName, scrapper.StateCode);

                    if (!await scrapper.IsSourceAvailableAsync())
                    {
                        _logger.LogWarning("{AssemblyName} source is not available, skipping", scrapper.AssemblyName);
                        continue;
                    }

                    var deputies = await scrapper.FetchDeputiesAsync();

                    if (deputies.Count == 0)
                    {
                        _logger.LogWarning("No deputies fetched from {AssemblyName}", scrapper.AssemblyName);
                        continue;
                    }

                    var processedCount = await persistenceService.SaveStateDeputiesAsync(deputies, scrapper.StateCode);
                    totalProcessed += processedCount;

                    _logger.LogInformation("Processed {Count} deputies from {AssemblyName}", processedCount, scrapper.AssemblyName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing {AssemblyName}, skipping to next", scrapper.AssemblyName);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("State deputy sync complete: {Total} total deputies processed in {Duration}ms",
                totalProcessed, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "State deputy synchronization failed after {Duration}ms", duration.TotalMilliseconds);
            throw;
        }
    }

    private TimeSpan CalculateInitialDelay()
    {
        var now = DateTime.Now.TimeOfDay;
        var targetTime = _syncOptions.SyncStartTime;

        return now <= targetTime
            ? targetTime - now
            : TimeSpan.FromHours(24) - now + targetTime;
    }
}

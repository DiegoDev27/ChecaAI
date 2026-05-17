using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChecaAI.Worker.Configuration;

namespace ChecaAI.Worker.Services;

public class ChamberDataSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChamberDataSyncService> _logger;
    private readonly DataSyncOptions _syncOptions;
    private Timer? _scheduledTimer;

    public ChamberDataSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<ChamberDataSyncService> logger,
        IOptions<DataSyncOptions> syncOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _syncOptions = syncOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Chamber Data Sync Service started");

        if (!_syncOptions.EnableScheduledSync)
        {
            _logger.LogInformation("Scheduled sync is disabled. Chamber service will run once and exit.");
            await RunSingleSyncAsync();
            return;
        }

        var initialDelay = CalculateInitialDelay();
        _logger.LogInformation("Chamber scheduled sync will start in {Delay} and repeat every {Interval}",
            initialDelay, _syncOptions.ChamberDataSyncInterval);

        _scheduledTimer = new Timer(async _ => await ExecuteSyncWithRetry(),
            null, initialDelay, _syncOptions.ChamberDataSyncInterval);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chamber Data Sync Service is shutting down");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chamber Data Sync Service is stopping");
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
            _logger.LogError(ex, "Failed to complete single Chamber sync operation");
        }
    }

    private async Task ExecuteSyncWithRetry()
    {
        var attempt = 1;

        while (attempt <= _syncOptions.RetryAttempts)
        {
            try
            {
                _logger.LogInformation("Starting Chamber data synchronization - Attempt {Attempt}/{MaxAttempts}",
                    attempt, _syncOptions.RetryAttempts);

                await ExecuteSyncAsync();

                _logger.LogInformation("Chamber data synchronization completed successfully");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chamber data synchronization failed on attempt {Attempt}/{MaxAttempts}",
                    attempt, _syncOptions.RetryAttempts);

                if (attempt == _syncOptions.RetryAttempts)
                {
                    _logger.LogError("All retry attempts exhausted. Chamber data synchronization failed definitively");
                    return;
                }

                attempt++;
                _logger.LogInformation("Waiting {Delay} before retry attempt {Attempt}",
                    _syncOptions.RetryDelay, attempt);

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
            var scrapperService = scope.ServiceProvider.GetRequiredService<IChamberScrapperService>();
            var persistenceService = scope.ServiceProvider.GetRequiredService<IChamberPersistenceService>();

            if (!await scrapperService.IsApiAvailableAsync())
                throw new InvalidOperationException("Chamber API is not available");

            _logger.LogInformation("Fetching data from Chamber API...");
            var deputies = await scrapperService.FetchFederalDeputiesAsync();

            if (deputies.Count == 0)
            {
                _logger.LogWarning("No federal deputies received from Chamber API");
                return;
            }

            _logger.LogInformation("Successfully fetched {Count} federal deputies from API", deputies.Count);

            var processedCount = await persistenceService.SaveFederalDeputiesAsync(deputies);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Successfully processed {ProcessedCount} federal deputies in {Duration}ms",
                processedCount, duration.TotalMilliseconds);

            _logger.LogInformation("Sync Statistics: {{ FetchedCount: {FetchedCount}, ProcessedCount: {ProcessedCount}, Duration: {Duration} }}",
                deputies.Count, processedCount, duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Chamber data synchronization failed after {Duration}ms", duration.TotalMilliseconds);
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

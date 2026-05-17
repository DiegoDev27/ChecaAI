using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChecaAI.Worker.Configuration;

namespace ChecaAI.Worker.Services;

public class SenateDataSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SenateDataSyncService> _logger;
    private readonly DataSyncOptions _syncOptions;
    private Timer? _scheduledTimer;

    public SenateDataSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<SenateDataSyncService> logger,
        IOptions<DataSyncOptions> syncOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _syncOptions = syncOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Senate Data Sync Service started");

        if (!_syncOptions.EnableScheduledSync)
        {
            _logger.LogInformation("Scheduled sync is disabled. Service will run once and exit.");
            await RunSingleSyncAsync();
            return;
        }

        // Calculate initial delay to start at configured time
        var initialDelay = CalculateInitialDelay();
        _logger.LogInformation("Scheduled sync will start in {Delay} and repeat every {Interval}", 
            initialDelay, _syncOptions.SenateDataSyncInterval);

        // Set up timer for scheduled execution
        _scheduledTimer = new Timer(async _ => await ExecuteSyncWithRetry(), 
            null, initialDelay, _syncOptions.SenateDataSyncInterval);

        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Senate Data Sync Service is shutting down");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Senate Data Sync Service is stopping");
        
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
            _logger.LogError(ex, "Failed to complete single sync operation");
        }
    }

    private async Task ExecuteSyncWithRetry()
    {
        var attempt = 1;
        
        while (attempt <= _syncOptions.RetryAttempts)
        {
            try
            {
                _logger.LogInformation("Starting Senate data synchronization - Attempt {Attempt}/{MaxAttempts}", 
                    attempt, _syncOptions.RetryAttempts);

                await ExecuteSyncAsync();
                
                _logger.LogInformation("Senate data synchronization completed successfully");
                return; // Success, no need to retry
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Senate data synchronization failed on attempt {Attempt}/{MaxAttempts}", 
                    attempt, _syncOptions.RetryAttempts);

                if (attempt == _syncOptions.RetryAttempts)
                {
                    _logger.LogError("All retry attempts exhausted. Senate data synchronization failed definitively");
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
            // Create a scope to access scoped services
            using var scope = _scopeFactory.CreateScope();
            var scrapperService = scope.ServiceProvider.GetRequiredService<ISenateScrapperService>();
            var persistenceService = scope.ServiceProvider.GetRequiredService<IDataPersistenceService>();

            // Check if Senate API is available
            if (!await scrapperService.IsApiAvailableAsync())
            {
                throw new InvalidOperationException("Senate API is not available");
            }

            // Fetch data from Senate API
            _logger.LogInformation("Fetching data from Senate API...");
            var senateData = await scrapperService.FetchSenatorsDataAsync();
            
            var parliamentarians = senateData?.ListaParlamentarEmExercicio?.Parlamentares?.Parlamentar;
            if (senateData == null || parliamentarians == null || !parliamentarians.Any())
            {
                _logger.LogWarning("No data received from Senate API or empty response");
                return;
            }

            _logger.LogInformation("Successfully fetched {Count} senators from API", 
                parliamentarians.Count);

            // Save data to database
            _logger.LogInformation("Saving data to database...");
            var processedCount = await persistenceService.SaveSenatorsDataAsync(senateData);
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Successfully processed {ProcessedCount} senators in {Duration}ms", 
                processedCount, duration.TotalMilliseconds);

            // Log sync statistics
            LogSyncStatistics(senateData, processedCount, duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Senate data synchronization failed after {Duration}ms", duration.TotalMilliseconds);
            throw;
        }
    }

    private TimeSpan CalculateInitialDelay()
    {
        var now = DateTime.Now.TimeOfDay;
        var targetTime = _syncOptions.SyncStartTime;

        if (now <= targetTime)
        {
            // Start today
            return targetTime - now;
        }
        else
        {
            // Start tomorrow
            return TimeSpan.FromHours(24) - now + targetTime;
        }
    }

    private void LogSyncStatistics(Models.DTOs.SenateApiResponse senateData, int processedCount, TimeSpan duration)
    {
        try
        {
            var parliamentarians = senateData?.ListaParlamentarEmExercicio?.Parlamentares?.Parlamentar ?? new List<Models.DTOs.ParlamentarDto>();
            var metadata = senateData?.ListaParlamentarEmExercicio?.Metadados;
            
            var statistics = new
            {
                SyncTime = DateTime.UtcNow,
                Duration = duration,
                FetchedCount = parliamentarians.Count,
                ProcessedCount = processedCount,
                ApiVersion = metadata?.Versao ?? "Unknown",
                ServiceVersion = metadata?.VersaoServico ?? "Unknown",
                DataSetDescription = metadata?.DescricaoDataSet ?? "Unknown"
            };

            _logger.LogInformation("Sync Statistics: {@Statistics}", statistics);

            // Log additional metrics for monitoring
            var senatorsWithPhones = parliamentarians
                .Count(p => p.IdentificacaoParlamentar.Telefones?.Telefone.Any() == true);
            
            var senatorsWithBlocs = parliamentarians
                .Count(p => p.IdentificacaoParlamentar.Bloco != null);

            var senatorsWithMandates = parliamentarians
                .Count(p => p.Mandato.PrimeiraLegislaturaDoMandato != null || p.Mandato.SegundaLegislaturaDoMandato != null);

            _logger.LogInformation("Data Quality Metrics: Senators with phones: {PhonesCount}, " +
                                  "with political blocs: {BlocsCount}, with mandates: {MandatesCount}",
                senatorsWithPhones, senatorsWithBlocs, senatorsWithMandates);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log sync statistics");
        }
    }
}
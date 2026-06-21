using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChecaAI.Application.Interfaces;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Worker.Configuration;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Background service that syncs all 513 federal deputies from the Câmara API
/// into the Politicians table. Runs once at startup then repeats every SyncInterval.
/// </summary>
public class ChamberDeputySyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChamberDeputySyncService> _logger;
    private readonly ChamberSyncOptions _options;

    private const string Position = "Federal Deputy";

    public ChamberDeputySyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<ChamberDeputySyncService> logger,
        IOptions<ChamberSyncOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[ChamberDeputySync] Disabled via configuration, skipping");
            return;
        }

        _logger.LogInformation("[ChamberDeputySync] Started — syncing every {Interval}",
            _options.SyncInterval);

        // Run immediately on startup, then on the configured interval
        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncDeputiesAsync(stoppingToken);
            await Task.Delay(_options.SyncInterval, stoppingToken).ContinueWith(_ => { });
        }

        _logger.LogInformation("[ChamberDeputySync] Stopped");
    }

    private async Task SyncDeputiesAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chamberService = scope.ServiceProvider.GetRequiredService<IChamberOfDeputiesService>();
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

            _logger.LogInformation("[ChamberDeputySync] Fetching deputies from Câmara API...");
            var deputies = (await chamberService.GetDeputiesAsync()).ToList();

            if (deputies.Count == 0)
            {
                _logger.LogWarning("[ChamberDeputySync] No deputies returned from API");
                return;
            }

            _logger.LogInformation("[ChamberDeputySync] Fetched {Count} deputies", deputies.Count);

            // Load existing deputies keyed by ExternalId for fast upsert
            var existingByExternalId = await db.Politicians
                .Where(p => p.PoliticalPosition == Position)
                .ToDictionaryAsync(p => p.ExternalId, ct);

            var added = 0;
            var updated = 0;

            foreach (var deputy in deputies)
            {
                if (string.IsNullOrWhiteSpace(deputy.ExternalId)) continue;

                if (existingByExternalId.TryGetValue(deputy.ExternalId, out var existing))
                {
                    // Update mutable fields
                    existing.FullName = deputy.FullName;
                    existing.Party = deputy.Party;
                    existing.State = deputy.State;
                    existing.PhotoUrl = deputy.PhotoUrl;
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    deputy.CreatedAt = DateTime.UtcNow;
                    deputy.UpdatedAt = DateTime.UtcNow;
                    db.Politicians.Add(deputy);
                    added++;
                }
            }

            await db.SaveChangesAsync(ct);
            sw.Stop();

            _logger.LogInformation(
                "[ChamberDeputySync] Done in {Ms}ms — added {Added}, updated {Updated} deputies",
                sw.ElapsedMilliseconds, added, updated);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogError(ex, "[ChamberDeputySync] Sync failed after {Ms}ms", sw.ElapsedMilliseconds);
        }
    }
}

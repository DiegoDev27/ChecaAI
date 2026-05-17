using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services.StateDeputy;

public interface IStateDeputyPersistenceService
{
    Task<int> SaveStateDeputiesAsync(List<StateDeputyData> deputies, string stateCode);
    Task<bool> StateDeputyExistsAsync(string externalId, string stateCode);
}

public class StateDeputyPersistenceService : IStateDeputyPersistenceService
{
    private const string StateDeputyPosition = "State Deputy";

    private readonly ChecaAIDbContext _context;
    private readonly ILogger<StateDeputyPersistenceService> _logger;

    public StateDeputyPersistenceService(ChecaAIDbContext context, ILogger<StateDeputyPersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SaveStateDeputiesAsync(List<StateDeputyData> deputies, string stateCode)
    {
        var savedCount = 0;
        var updatedCount = 0;

        try
        {
            _logger.LogInformation("Starting to save {Count} state deputies ({State}) to database",
                deputies.Count, stateCode);

            foreach (var deputy in deputies)
            {
                try
                {
                    var isNew = await ProcessDeputyAsync(deputy, stateCode);
                    if (isNew) savedCount++;
                    else updatedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing state deputy {Name} ({State})",
                        deputy.FullName, stateCode);
                }
            }

            // Deactivate deputies no longer in the current scrape (e.g. became prefeito, retired, left office)
            var processedIds = deputies.Select(d => d.ExternalId).ToHashSet();
            var stale = await _context.Politicians
                .Where(p => p.State == stateCode
                         && p.PoliticalPosition == StateDeputyPosition
                         && p.IsActive
                         && !processedIds.Contains(p.ExternalId!))
                .ToListAsync();

            foreach (var s in stale)
            {
                s.IsActive = false;
                s.UpdatedAt = DateTime.UtcNow;
            }

            if (stale.Count > 0)
                _logger.LogInformation("Deactivated {Count} stale state deputies for {State}", stale.Count, stateCode);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully processed {State} state deputies: {SavedCount} new, {UpdatedCount} updated, {DeactivatedCount} deactivated",
                stateCode, savedCount, updatedCount, stale.Count);

            return savedCount + updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving state deputies ({State}) to database", stateCode);
            throw;
        }
    }

    public async Task<bool> StateDeputyExistsAsync(string externalId, string stateCode)
    {
        return await _context.Politicians
            .AnyAsync(p => p.ExternalId == externalId
                        && p.PoliticalPosition == StateDeputyPosition
                        && p.State == stateCode);
    }

    private async Task<bool> ProcessDeputyAsync(StateDeputyData data, string stateCode)
    {
        var existing = await _context.Politicians
            .FirstOrDefaultAsync(p => p.ExternalId == data.ExternalId
                                   && p.PoliticalPosition == StateDeputyPosition
                                   && p.State == stateCode);

        if (existing == null)
        {
            _context.Politicians.Add(CreateFromData(data));
            return true;
        }
        else
        {
            UpdateFromData(existing, data);
            return false;
        }
    }

    private static Politician CreateFromData(StateDeputyData data)
    {
        return new Politician
        {
            ExternalId = data.ExternalId,
            FullName = data.FullName,
            PoliticalPosition = StateDeputyPosition,
            Party = data.Party,
            State = data.StateCode,
            Email = data.Email,
            PhotoUrl = data.PhotoUrl,
            ParlamentaryPageUrl = data.ParlamentaryPageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void UpdateFromData(Politician politician, StateDeputyData data)
    {
        politician.FullName = data.FullName;
        if (data.Party != null)
            politician.Party = data.Party;
        politician.Email = data.Email;
        politician.PhotoUrl = data.PhotoUrl;
        politician.ParlamentaryPageUrl = data.ParlamentaryPageUrl;
        politician.IsActive = true;
        politician.UpdatedAt = DateTime.UtcNow;
    }
}

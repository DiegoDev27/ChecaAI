using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Infrastructure.DTOs;

namespace ChecaAI.Worker.Services;

public interface IChamberPersistenceService
{
    Task<int> SaveFederalDeputiesAsync(List<DeputyDto> deputies);
    Task<bool> FederalDeputyExistsAsync(string externalId);
    Task<Politician?> GetFederalDeputyByExternalIdAsync(string externalId);
}

public class ChamberDataPersistenceService : IChamberPersistenceService
{
    private const string FederalDeputyPosition = "Federal Deputy";

    private readonly ChecaAIDbContext _context;
    private readonly ILogger<ChamberDataPersistenceService> _logger;

    public ChamberDataPersistenceService(ChecaAIDbContext context, ILogger<ChamberDataPersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SaveFederalDeputiesAsync(List<DeputyDto> deputies)
    {
        var savedCount = 0;
        var updatedCount = 0;

        try
        {
            _logger.LogInformation("Starting to save {Count} federal deputies to database", deputies.Count);

            foreach (var dto in deputies)
            {
                try
                {
                    var isNew = await ProcessDeputyAsync(dto);
                    if (isNew) savedCount++;
                    else updatedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing federal deputy with ID {Id}", dto.Id);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully processed federal deputies: {SavedCount} new, {UpdatedCount} updated",
                savedCount, updatedCount);

            return savedCount + updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving federal deputies to database");
            throw;
        }
    }

    public async Task<bool> FederalDeputyExistsAsync(string externalId)
    {
        return await _context.Politicians
            .AnyAsync(p => p.ExternalId == externalId && p.PoliticalPosition == FederalDeputyPosition);
    }

    public async Task<Politician?> GetFederalDeputyByExternalIdAsync(string externalId)
    {
        return await _context.Politicians
            .FirstOrDefaultAsync(p => p.ExternalId == externalId && p.PoliticalPosition == FederalDeputyPosition);
    }

    private async Task<bool> ProcessDeputyAsync(DeputyDto dto)
    {
        var externalId = dto.Id.ToString();
        var existing = await GetFederalDeputyByExternalIdAsync(externalId);

        if (existing == null)
        {
            var politician = CreateFromDto(dto);
            _context.Politicians.Add(politician);
            return true;
        }
        else
        {
            UpdateFromDto(existing, dto);
            return false;
        }
    }

    private static Politician CreateFromDto(DeputyDto dto)
    {
        return new Politician
        {
            ExternalId = dto.Id.ToString(),
            FullName = dto.Name,
            PoliticalPosition = FederalDeputyPosition,
            Party = dto.Party,
            State = dto.State,
            Email = dto.Email,
            PhotoUrl = dto.PhotoUrl,
            Cpf = dto.Cpf,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void UpdateFromDto(Politician politician, DeputyDto dto)
    {
        politician.FullName = dto.Name;
        politician.Party = dto.Party;
        politician.State = dto.State;
        politician.Email = dto.Email;
        politician.PhotoUrl = dto.PhotoUrl;
        politician.Cpf = dto.Cpf;
        politician.IsActive = true;
        politician.UpdatedAt = DateTime.UtcNow;
    }
}

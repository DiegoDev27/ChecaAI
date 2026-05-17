using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Worker.Models.DTOs;

namespace ChecaAI.Worker.Services;

public interface IDataPersistenceService
{
    Task<int> SaveSenatorsDataAsync(SenateApiResponse senateData);
    Task<bool> SenatorExistsAsync(string externalId);
    Task<Politician?> GetSenatorByExternalIdAsync(string externalId);
}

public class DataPersistenceService : IDataPersistenceService
{
    private readonly ChecaAIDbContext _context;
    private readonly ILogger<DataPersistenceService> _logger;

    public DataPersistenceService(ChecaAIDbContext context, ILogger<DataPersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SaveSenatorsDataAsync(SenateApiResponse senateData)
    {
        var savedCount = 0;
        var updatedCount = 0;

        try
        {
            var parliamentarians = senateData.ListaParlamentarEmExercicio?.Parlamentares?.Parlamentar ?? new List<ParlamentarDto>();
            _logger.LogInformation("Starting to save {Count} senators to database", parliamentarians.Count);

            foreach (var parliamentarianDto in parliamentarians)
            {
                try
                {
                    var result = await ProcessSenatorAsync(parliamentarianDto);
                    if (result.IsNew)
                        savedCount++;
                    else
                        updatedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing senator with code {Code}", 
                        parliamentarianDto.IdentificacaoParlamentar.CodigoParlamentar);
                }
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully processed senators: {SavedCount} new, {UpdatedCount} updated", 
                savedCount, updatedCount);
            
            return savedCount + updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving senators data to database");
            throw;
        }
    }

    public async Task<bool> SenatorExistsAsync(string externalId)
    {
        return await _context.Politicians
            .AnyAsync(p => p.ExternalId == externalId && p.PoliticalPosition == "Senator");
    }

    public async Task<Politician?> GetSenatorByExternalIdAsync(string externalId)
    {
        return await _context.Politicians
            .Include(p => p.PoliticalBloc)
            .Include(p => p.Phones)
            .Include(p => p.Mandates)
            .FirstOrDefaultAsync(p => p.ExternalId == externalId && p.PoliticalPosition == "Senator");
    }

    private async Task<(bool IsNew, Politician Politician)> ProcessSenatorAsync(ParlamentarDto dto)
    {
        var externalId = dto.IdentificacaoParlamentar.CodigoParlamentar;
        var existingSenator = await GetSenatorByExternalIdAsync(externalId);

        if (existingSenator == null)
        {
            // Create new senator
            var newSenator = await CreateSenatorFromDtoAsync(dto);
            _context.Politicians.Add(newSenator);
            return (true, newSenator);
        }
        else
        {
            // Update existing senator
            await UpdateSenatorFromDtoAsync(existingSenator, dto);
            return (false, existingSenator);
        }
    }

    private async Task<Politician> CreateSenatorFromDtoAsync(ParlamentarDto dto)
    {
        var identification = dto.IdentificacaoParlamentar;
        
        var politician = new Politician
        {
            ExternalId = identification.CodigoParlamentar,
            CurrentLegislaturePublicCode = identification.CodigoPublicoNaLegAtual,
            ParlamentaryName = identification.NomeParlamentar,
            FullName = identification.NomeCompletoParlamentar,
            Gender = identification.SexoParlamentar,
            Treatment = identification.FormaTratamento,
            PhotoUrl = identification.UrlFotoParlamentar,
            ParlamentaryPageUrl = identification.UrlPaginaParlamentar,
            PersonalPageUrl = identification.UrlPaginaParticular,
            Email = identification.EmailParlamentar,
            Party = identification.SiglaPartidoParlamentar,
            State = identification.UfParlamentar,
            IsBoardMember = identification.MembroMesa == "Sim",
            IsLeadershipMember = identification.MembroLideranca == "Sim",
            PoliticalPosition = "Senator",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Process political bloc
        if (identification.Bloco != null)
        {
            politician.PoliticalBloc = await ProcessPoliticalBlocAsync(identification.Bloco);
        }

        // Process phones
        if (identification.Telefones != null && identification.Telefones.Telefone.Any())
        {
            foreach (var phoneDto in identification.Telefones.Telefone)
            {
                politician.Phones.Add(new PoliticianPhone
                {
                    PhoneNumber = phoneDto.NumeroTelefone,
                    PublicationOrder = int.TryParse(phoneDto.OrdemPublicacao, out var order) ? order : 1,
                    IsFax = phoneDto.IndicadorFax == "Sim"
                });
            }
        }

        // Process mandate
        var mandateEntity = await ProcessMandateAsync(dto.Mandato);
        politician.Mandates.Add(mandateEntity);

        return politician;
    }

    private async Task UpdateSenatorFromDtoAsync(Politician existingSenator, ParlamentarDto dto)
    {
        var identification = dto.IdentificacaoParlamentar;

        // Update basic information
        existingSenator.ParlamentaryName = identification.NomeParlamentar;
        existingSenator.FullName = identification.NomeCompletoParlamentar;
        existingSenator.Email = identification.EmailParlamentar;
        existingSenator.Party = identification.SiglaPartidoParlamentar;
        existingSenator.State = identification.UfParlamentar;
        existingSenator.IsBoardMember = identification.MembroMesa == "Sim";
        existingSenator.IsLeadershipMember = identification.MembroLideranca == "Sim";
        existingSenator.PhotoUrl = identification.UrlFotoParlamentar;
        existingSenator.UpdatedAt = DateTime.UtcNow;

        // Update political bloc if needed
        if (identification.Bloco != null)
        {
            var bloc = await ProcessPoliticalBlocAsync(identification.Bloco);
            existingSenator.PoliticalBlocId = bloc.Id;
        }

        // Note: For simplicity, we're not updating phones and mandates
        // In a production environment, you'd want more sophisticated sync logic
        _logger.LogDebug("Updated senator {Name} (ID: {ExternalId})", 
            existingSenator.FullName, existingSenator.ExternalId);
    }

    private async Task<PoliticalBloc> ProcessPoliticalBlocAsync(BlocoDto blocDto)
    {
        var code = int.TryParse(blocDto.CodigoBloco, out var codeValue) ? codeValue : 0;
        
        var existingBloc = await _context.PoliticalBlocs
            .FirstOrDefaultAsync(b => b.Code == code);

        if (existingBloc == null)
        {
            var newBloc = new PoliticalBloc
            {
                Code = code,
                Name = blocDto.NomeBloco,
                Nickname = blocDto.NomeApelido,
                CreationDate = DateOnly.TryParse(blocDto.DataCriacao, out var creationDate) 
                    ? creationDate : DateOnly.MinValue
            };

            _context.PoliticalBlocs.Add(newBloc);
            await _context.SaveChangesAsync(); // Save immediately to get the ID
            return newBloc;
        }

        return existingBloc;
    }

    private async Task<PoliticianMandate> ProcessMandateAsync(MandatoDto mandateDto)
    {
        var mandate = new PoliticianMandate
        {
            MandateCode = int.TryParse(mandateDto.CodigoMandato, out var mandateCode) ? mandateCode : 0,
            State = mandateDto.UfParlamentar,
            ParticipationDescription = mandateDto.DescricaoParticipacao
        };

        // Process legislatures
        if (mandateDto.PrimeiraLegislaturaDoMandato != null)
        {
            mandate.Legislatures.Add(CreateLegislature(mandateDto.PrimeiraLegislaturaDoMandato, "Primeira"));
        }

        if (mandateDto.SegundaLegislaturaDoMandato != null)
        {
            mandate.Legislatures.Add(CreateLegislature(mandateDto.SegundaLegislaturaDoMandato, "Segunda"));
        }

        // Process exercises
        if (mandateDto.Exercicios?.Exercicio != null)
        {
            foreach (var exerciseDto in mandateDto.Exercicios.Exercicio)
            {
                mandate.Exercises.Add(new MandateExercise
                {
                    ExerciseCode = int.TryParse(exerciseDto.CodigoExercicio, out var exCode) ? exCode : 0,
                    StartDate = DateOnly.TryParse(exerciseDto.DataInicio, out var startEx) ? startEx : DateOnly.MinValue,
                    EndDate = DateOnly.TryParse(exerciseDto.DataFim, out var endEx) ? endEx : null,
                    LeaveReasonCode = exerciseDto.SiglaCausaAfastamento,
                    LeaveReasonDescription = exerciseDto.DescricaoCausaAfastamento,
                    ReadingDate = DateOnly.TryParse(exerciseDto.DataLeitura, out var readingDate) ? readingDate : null
                });
            }
        }

        // Note: Substitutes processing could be added here if needed
        // For now, we're focusing on the main senator data

        await Task.CompletedTask; // Placeholder for async operations
        return mandate;
    }

    private Legislature CreateLegislature(LegislaturaDto dto, string type)
    {
        return new Legislature
        {
            Number = int.TryParse(dto.NumeroLegislatura, out var num) ? num : 0,
            StartDate = DateOnly.TryParse(dto.DataInicio, out var start) ? start : DateOnly.MinValue,
            EndDate = DateOnly.TryParse(dto.DataFim, out var end) ? end : DateOnly.MinValue,
            LegislatureType = type
        };
    }
}
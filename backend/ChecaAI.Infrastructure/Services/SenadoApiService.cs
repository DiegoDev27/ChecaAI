using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Infrastructure.Services;

public class SenateApiService : ISenateService
{
    private readonly HttpClient _httpClient;
    private readonly ChecaAIDbContext _context;
    private readonly ILogger<SenateApiService> _logger;
    private const string SENATE_API_BASE_URL = "https://legis.senado.leg.br/dadosabertos";

    public SenateApiService(
        HttpClient httpClient,
        ChecaAIDbContext context,
        ILogger<SenateApiService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Politician>> GetSenatorsAsync()
    {
        try
        {
            var url = $"{SENATE_API_BASE_URL}/senador/lista/atual";
            var xmlContent = await _httpClient.GetStringAsync(url);
            
            var politicians = await ParseSenatorsFromXmlAsync(xmlContent);
            
            // Store or update senators in database
            await SaveSenatorsToDatabase(politicians);
            
            return politicians;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching senators from Senate API");
            throw;
        }
    }

    public async Task<Politician?> GetSenatorByIdAsync(string externalId)
    {
        return await _context.Politicians
            .Include(p => p.PoliticalBloc)
            .Include(p => p.Phones)
            .Include(p => p.Mandates)
                .ThenInclude(m => m.Legislatures)
            .Include(p => p.Mandates)
                .ThenInclude(m => m.Substitutes)
            .Include(p => p.Mandates)
                .ThenInclude(m => m.Exercises)
            .FirstOrDefaultAsync(p => p.ExternalId == externalId);
    }

    public async Task<IEnumerable<Proposal>> GetProposalsAsync(int? year = null, string? type = null)
    {
        // TODO: Implement Senate proposals API integration
        await Task.CompletedTask;
        return new List<Proposal>();
    }

    public async Task<Proposal?> GetProposalByIdAsync(string externalId)
    {
        // TODO: Implement Senate proposal by ID API integration
        await Task.CompletedTask;
        return null;
    }

    public async Task<IEnumerable<VotingSession>> GetVotingSessionsByProposalAsync(string proposalId)
    {
        // TODO: Implement Senate voting sessions API integration
        await Task.CompletedTask;
        return new List<VotingSession>();
    }

    public async Task<IEnumerable<Vote>> GetVotesByVotingSessionAsync(string votingSessionId)
    {
        // TODO: Implement Senate votes API integration
        await Task.CompletedTask;
        return new List<Vote>();
    }

    private async Task<List<Politician>> ParseSenatorsFromXmlAsync(string xmlContent)
    {
        var politicians = new List<Politician>();
        var xml = XDocument.Parse(xmlContent);
        
        var parlamentares = xml.Descendants("Parlamentar");
        
        foreach (var parlamentar in parlamentares)
        {
            var identificacao = parlamentar.Element("IdentificacaoParlamentar");
            var mandato = parlamentar.Element("Mandato");
            
            if (identificacao == null || mandato == null) continue;

            var politician = new Politician
            {
                ExternalId = identificacao.Element("CodigoParlamentar")?.Value,
                CurrentLegislaturePublicCode = identificacao.Element("CodigoPublicoNaLegAtual")?.Value,
                ParlamentaryName = identificacao.Element("NomeParlamentar")?.Value,
                FullName = identificacao.Element("NomeCompletoParlamentar")?.Value ?? string.Empty,
                Gender = identificacao.Element("SexoParlamentar")?.Value,
                Treatment = identificacao.Element("FormaTratamento")?.Value,
                PhotoUrl = identificacao.Element("UrlFotoParlamentar")?.Value,
                ParlamentaryPageUrl = identificacao.Element("UrlPaginaParlamentar")?.Value,
                PersonalPageUrl = identificacao.Element("UrlPaginaParticular")?.Value,
                Email = identificacao.Element("EmailParlamentar")?.Value,
                Party = identificacao.Element("SiglaPartidoParlamentar")?.Value,
                State = identificacao.Element("UfParlamentar")?.Value,
                IsBoardMember = identificacao.Element("MembroMesa")?.Value == "Sim",
                IsLeadershipMember = identificacao.Element("MembroLideranca")?.Value == "Sim",
                PoliticalPosition = "Senator",
                IsActive = true
            };

            // Parse phones
            var telefones = identificacao.Element("Telefones")?.Elements("Telefone");
            if (telefones != null)
            {
                foreach (var telefone in telefones)
                {
                    politician.Phones.Add(new PoliticianPhone
                    {
                        PhoneNumber = telefone.Element("NumeroTelefone")?.Value ?? string.Empty,
                        PublicationOrder = int.TryParse(telefone.Element("OrdemPublicacao")?.Value, out var ordem) ? ordem : 1,
                        IsFax = telefone.Element("IndicadorFax")?.Value == "Sim"
                    });
                }
            }

            // Parse political bloc
            var bloco = identificacao.Element("Bloco");
            if (bloco != null)
            {
                var blocCode = int.TryParse(bloco.Element("CodigoBloco")?.Value, out var code) ? code : 0;
                var existingBloc = await _context.PoliticalBlocs.FirstOrDefaultAsync(b => b.Code == blocCode);
                
                if (existingBloc == null)
                {
                    var newBloc = new PoliticalBloc
                    {
                        Code = blocCode,
                        Name = bloco.Element("NomeBloco")?.Value ?? string.Empty,
                        Nickname = bloco.Element("NomeApelido")?.Value,
                        CreationDate = DateOnly.TryParse(bloco.Element("DataCriacao")?.Value, out var creationDate) ? creationDate : DateOnly.MinValue
                    };
                    politician.PoliticalBloc = newBloc;
                }
                else
                {
                    politician.PoliticalBlocId = existingBloc.Id;
                }
            }

            // Parse mandate
            var mandate = new PoliticianMandate
            {
                MandateCode = int.TryParse(mandato.Element("CodigoMandato")?.Value, out var mandateCode) ? mandateCode : 0,
                State = mandato.Element("UfParlamentar")?.Value ?? string.Empty,
                ParticipationDescription = mandato.Element("DescricaoParticipacao")?.Value ?? string.Empty
            };

            // Parse legislatures
            var primeiraLegislatura = mandato.Element("PrimeiraLegislaturaDoMandato");
            if (primeiraLegislatura != null)
            {
                mandate.Legislatures.Add(new Legislature
                {
                    Number = int.TryParse(primeiraLegislatura.Element("NumeroLegislatura")?.Value, out var num1) ? num1 : 0,
                    StartDate = DateOnly.TryParse(primeiraLegislatura.Element("DataInicio")?.Value, out var start1) ? start1 : DateOnly.MinValue,
                    EndDate = DateOnly.TryParse(primeiraLegislatura.Element("DataFim")?.Value, out var end1) ? end1 : DateOnly.MinValue,
                    LegislatureType = "Primeira"
                });
            }

            var segundaLegislatura = mandato.Element("SegundaLegislaturaDoMandato");
            if (segundaLegislatura != null)
            {
                mandate.Legislatures.Add(new Legislature
                {
                    Number = int.TryParse(segundaLegislatura.Element("NumeroLegislatura")?.Value, out var num2) ? num2 : 0,
                    StartDate = DateOnly.TryParse(segundaLegislatura.Element("DataInicio")?.Value, out var start2) ? start2 : DateOnly.MinValue,
                    EndDate = DateOnly.TryParse(segundaLegislatura.Element("DataFim")?.Value, out var end2) ? end2 : DateOnly.MinValue,
                    LegislatureType = "Segunda"
                });
            }

            // Parse exercises
            var exercicios = mandato.Element("Exercicios")?.Elements("Exercicio");
            if (exercicios != null)
            {
                foreach (var exercicio in exercicios)
                {
                    mandate.Exercises.Add(new MandateExercise
                    {
                        ExerciseCode = int.TryParse(exercicio.Element("CodigoExercicio")?.Value, out var exCode) ? exCode : 0,
                        StartDate = DateOnly.TryParse(exercicio.Element("DataInicio")?.Value, out var startEx) ? startEx : DateOnly.MinValue,
                        EndDate = DateOnly.TryParse(exercicio.Element("DataFim")?.Value, out var endEx) ? endEx : null,
                        LeaveReasonCode = exercicio.Element("SiglaCausaAfastamento")?.Value,
                        LeaveReasonDescription = exercicio.Element("DescricaoCausaAfastamento")?.Value,
                        ReadingDate = DateOnly.TryParse(exercicio.Element("DataLeitura")?.Value, out var readingDate) ? readingDate : null
                    });
                }
            }

            politician.Mandates.Add(mandate);
            politicians.Add(politician);
        }

        return politicians;
    }

    private async Task SaveSenatorsToDatabase(List<Politician> politicians)
    {
        foreach (var politician in politicians)
        {
            var existingPolitician = await _context.Politicians
                .FirstOrDefaultAsync(p => p.ExternalId == politician.ExternalId);

            if (existingPolitician == null)
            {
                _context.Politicians.Add(politician);
            }
            else
            {
                // Update existing politician data
                existingPolitician.FullName = politician.FullName;
                existingPolitician.ParlamentaryName = politician.ParlamentaryName;
                existingPolitician.Email = politician.Email;
                existingPolitician.Party = politician.Party;
                existingPolitician.State = politician.State;
                existingPolitician.IsBoardMember = politician.IsBoardMember;
                existingPolitician.IsLeadershipMember = politician.IsLeadershipMember;
                existingPolitician.UpdatedAt = DateTime.UtcNow;
                
                // Note: For simplicity, this doesn't update related entities
                // In a production environment, you'd want more sophisticated sync logic
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Successfully processed {politicians.Count} senators");
    }
}
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.DTOs;

namespace ChecaAI.Infrastructure.Services;

public class ChamberOfDeputiesService : IChamberOfDeputiesService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<ChamberOfDeputiesService> _logger;

    public ChamberOfDeputiesService(HttpClient httpClient, IConfiguration configuration, ILogger<ChamberOfDeputiesService> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["GovernmentApis:ChamberOfDeputiesBaseUrl"] ?? "https://dadosabertos.camara.leg.br/api/v2";
        _logger = logger;
    }

    public async Task<IEnumerable<Politician>> GetDeputiesAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/deputados");
            var apiResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<DeputyDto>>(response);

            return apiResponse?.Data.Select(MapToPolitician) ?? Enumerable.Empty<Politician>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching deputies from Chamber of Deputies API");
            return Enumerable.Empty<Politician>();
        }
    }

    public async Task<Politician?> GetDeputyByIdAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/deputados/{externalId}");
            var apiResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<DeputyDto>>(response);

            var deputyDto = apiResponse?.Data.FirstOrDefault();
            return deputyDto != null ? MapToPolitician(deputyDto) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching deputy {ExternalId} from Chamber of Deputies API", externalId);
            return null;
        }
    }

    public async Task<IEnumerable<Proposal>> GetProposalsAsync(int? year = null, string? type = null)
    {
        try
        {
            var url = $"{_baseUrl}/proposicoes";
            var queryParams = new List<string>();

            if (year.HasValue)
                queryParams.Add($"ano={year}");

            if (!string.IsNullOrEmpty(type))
                queryParams.Add($"siglaTipo={type}");

            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetStringAsync(url);
            var apiResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<ProposalDto>>(response);

            return apiResponse?.Data.Select(MapToProposal) ?? Enumerable.Empty<Proposal>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching proposals from Chamber of Deputies API");
            return Enumerable.Empty<Proposal>();
        }
    }

    public async Task<Proposal?> GetProposalByIdAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/proposicoes/{externalId}");
            var apiResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<ProposalDto>>(response);

            var proposalDto = apiResponse?.Data.FirstOrDefault();
            return proposalDto != null ? MapToProposal(proposalDto) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching proposal {ExternalId} from Chamber of Deputies API", externalId);
            return null;
        }
    }

    public async Task<IEnumerable<VotingSession>> GetVotingSessionsByProposalAsync(string proposalId)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/proposicoes/{proposalId}/votacoes");
            var apiResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<VotingSessionDto>>(response);

            return apiResponse?.Data.Select(dto => MapToVotingSession(dto, proposalId)) ?? Enumerable.Empty<VotingSession>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching voting sessions for proposal {ProposalId} from Chamber of Deputies API", proposalId);
            return Enumerable.Empty<VotingSession>();
        }
    }

    public async Task<IEnumerable<Vote>> GetVotesByVotingSessionAsync(string votingSessionId)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/votacoes/{votingSessionId}/votos");
            var apiResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<VoteDto>>(response);

            return apiResponse?.Data.Select(MapToVote) ?? Enumerable.Empty<Vote>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching votes for voting session {VotingSessionId} from Chamber of Deputies API", votingSessionId);
            return Enumerable.Empty<Vote>();
        }
    }

    public async Task<IEnumerable<PoliticianExpense>> GetDeputyExpensesAsync(string deputyId, int? year = null, int? month = null)
    {
        try
        {
            var url = $"{_baseUrl}/deputados/{deputyId}/despesas";
            var queryParams = new List<string>();

            if (year.HasValue)
                queryParams.Add($"ano={year}");

            if (month.HasValue)
                queryParams.Add($"mes={month}");

            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetStringAsync(url);
            var apiResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<ExpenseDto>>(response);

            return apiResponse?.Data.Select(dto => MapToExpense(dto, deputyId)) ?? Enumerable.Empty<PoliticianExpense>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expenses for deputy {DeputyId} from Chamber of Deputies API", deputyId);
            return Enumerable.Empty<PoliticianExpense>();
        }
    }

    private static Politician MapToPolitician(DeputyDto dto)
    {
        return new Politician
        {
            ExternalId = dto.Id.ToString(),
            FullName = dto.Name,
            PoliticalPosition = "Federal Deputy",
            Party = dto.Party,
            State = dto.State,
            PhotoUrl = dto.PhotoUrl,
            Cpf = dto.Cpf,
            IsActive = true
        };
    }

    private static Proposal MapToProposal(ProposalDto dto)
    {
        return new Proposal
        {
            ExternalId = dto.Id.ToString(),
            Title = dto.Summary,
            Type = dto.Type,
            Number = dto.Number.ToString(),
            Year = dto.Year,
            Chamber = "Chamber of Deputies",
            Summary = dto.Summary,
            Status = dto.Status?.Description ?? "Unknown",
            ProposalDate = dto.PresentationDate
        };
    }

    private static VotingSession MapToVotingSession(VotingSessionDto dto, string proposalId)
    {
        return new VotingSession
        {
            ExternalId = dto.Id,
            ProposalId = int.Parse(proposalId),
            Description = dto.Description,
            VotingDate = dto.Date,
            Result = dto.Approval == 1 ? "Approved" : "Rejected",
            Chamber = "Chamber of Deputies"
        };
    }

    private static Vote MapToVote(VoteDto dto)
    {
        return new Vote
        {
            PoliticianId = dto.Deputy.Id,
            VoteValue = dto.VoteType
        };
    }

    private static PoliticianExpense MapToExpense(ExpenseDto dto, string deputyId)
    {
        return new PoliticianExpense
        {
            PoliticianId = int.Parse(deputyId),
            Description = dto.ExpenseType,
            Category = dto.ExpenseType,
            Amount = dto.DocumentValue,
            Provider = dto.ProviderName,
            DocumentNumber = dto.DocumentNumber,
            ExpenseDate = dto.DocumentDate,
            Month = dto.Month.ToString(),
            Year = dto.Year,
            ExternalId = dto.DocumentCode.ToString()
        };
    }
}
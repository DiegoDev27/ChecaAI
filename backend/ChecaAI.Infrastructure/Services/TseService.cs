using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Infrastructure.Services;

public class TseService : ITseService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TseService> _logger;
    private const string TSE_API_BASE = "https://dadosabertos.tse.jus.br/api/3/action";

    public TseService(HttpClient httpClient, ILogger<TseService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<CampaignExpense>> GetCampaignExpensesAsync(string cpf, int electionYear)
    {
        try
        {
            // TSE dataset: despesas de campanha por CPF/CNPJ do candidato
            var url = $"{TSE_API_BASE}/datastore_search?resource_id=gastos-candidatos-{electionYear}&filters={{\"cpf_candidato\":\"{cpf}\"}}";
            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<TseApiResponse<TseCampaignExpenseDto>>(response, JsonOpts);

            return result?.Result?.Records.Select(r => new CampaignExpense
            {
                ElectionYear = electionYear,
                Category = r.Descricao ?? r.TipoDespesa ?? "Outros",
                Amount = r.ValorDespesa,
                Provider = r.NomeFornecedor,
                ProviderCnpj = r.CnpjCpfFornecedor,
                Description = r.Descricao,
                ExternalId = r.SqPrestacaoConta
            }) ?? Enumerable.Empty<CampaignExpense>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching campaign expenses from TSE for CPF {Cpf}", cpf);
            return Enumerable.Empty<CampaignExpense>();
        }
    }

    public async Task<IEnumerable<AssetDeclaration>> GetAssetDeclarationsAsync(string cpf, int electionYear)
    {
        try
        {
            // TSE dataset: bem declarado por candidato
            var url = $"{TSE_API_BASE}/datastore_search?resource_id=bem-candidato-{electionYear}&filters={{\"cpf_candidato\":\"{cpf}\"}}";
            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<TseApiResponse<TseAssetDto>>(response, JsonOpts);

            return result?.Result?.Records.Select(r => new AssetDeclaration
            {
                ElectionYear = electionYear,
                AssetType = r.DsBemCandidato ?? "Bem não classificado",
                DeclaredValue = r.VrBemCandidato,
                Description = r.DsBemCandidato,
                ExternalId = $"{cpf}-{electionYear}-{r.NrOrdemBem}"
            }) ?? Enumerable.Empty<AssetDeclaration>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching asset declarations from TSE for CPF {Cpf}", cpf);
            return Enumerable.Empty<AssetDeclaration>();
        }
    }

    public async Task<IEnumerable<ElectionResult>> GetElectionResultsAsync(string cpf)
    {
        // Query across several election years — TSE has data per election
        var years = new[] { 2022, 2020, 2018, 2016 };
        var allResults = new List<ElectionResult>();

        foreach (var year in years)
        {
            try
            {
                var url = $"{TSE_API_BASE}/datastore_search?resource_id=candidatos-{year}&filters={{\"cpf_candidato\":\"{cpf}\"}}";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<TseApiResponse<TseElectionResultDto>>(response, JsonOpts);

                var records = result?.Result?.Records ?? [];
                allResults.AddRange(records.Select(r => new ElectionResult
                {
                    ElectionYear = year,
                    Position = r.DsCargo ?? "Desconhecido",
                    State = r.SgUe?.Length == 2 ? r.SgUe : null,
                    City = r.NmMunicipio,
                    VotesReceived = r.QtVotos,
                    IsElected = r.DsSituacaoTurno?.Contains("ELEITO", StringComparison.OrdinalIgnoreCase) == true,
                    ExternalId = $"{cpf}-{year}-{r.SqCandidato}"
                }));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No TSE election data for year {Year}, CPF {Cpf}", year, cpf);
            }
        }

        return allResults;
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
}

// TSE API response wrapper
file class TseApiResponse<T>
{
    [JsonPropertyName("result")]
    public TseResult<T>? Result { get; set; }
}

file class TseResult<T>
{
    [JsonPropertyName("records")]
    public List<T> Records { get; set; } = [];
}

file class TseCampaignExpenseDto
{
    [JsonPropertyName("ds_origem_despesa")]
    public string? Descricao { get; set; }

    [JsonPropertyName("ds_despesa")]
    public string? TipoDespesa { get; set; }

    [JsonPropertyName("vr_despesa")]
    public decimal ValorDespesa { get; set; }

    [JsonPropertyName("nm_fornecedor")]
    public string? NomeFornecedor { get; set; }

    [JsonPropertyName("cd_cnpj_cpf_fornecedor")]
    public string? CnpjCpfFornecedor { get; set; }

    [JsonPropertyName("sq_prestacao_contas")]
    public string? SqPrestacaoConta { get; set; }
}

file class TseAssetDto
{
    [JsonPropertyName("ds_bem_candidato")]
    public string? DsBemCandidato { get; set; }

    [JsonPropertyName("vr_bem_candidato")]
    public decimal VrBemCandidato { get; set; }

    [JsonPropertyName("nr_ordem_bem")]
    public string? NrOrdemBem { get; set; }
}

file class TseElectionResultDto
{
    [JsonPropertyName("ds_cargo")]
    public string? DsCargo { get; set; }

    [JsonPropertyName("sg_ue")]
    public string? SgUe { get; set; }

    [JsonPropertyName("nm_municipio")]
    public string? NmMunicipio { get; set; }

    [JsonPropertyName("qt_votos_nominais")]
    public long QtVotos { get; set; }

    [JsonPropertyName("ds_situacao_turno")]
    public string? DsSituacaoTurno { get; set; }

    [JsonPropertyName("sq_candidato")]
    public string? SqCandidato { get; set; }
}

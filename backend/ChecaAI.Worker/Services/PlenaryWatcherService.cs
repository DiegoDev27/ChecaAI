using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Worker.Configuration;

namespace ChecaAI.Worker.Services;

public class PlenaryWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlenaryWatcherService> _logger;
    private readonly PlenaryWatcherOptions _options;
    private readonly HttpClient _httpClient;

    public PlenaryWatcherService(
        IServiceScopeFactory scopeFactory,
        ILogger<PlenaryWatcherService> logger,
        IOptions<PlenaryWatcherOptions> options,
        HttpClient httpClient)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[PlenaryWatcher] Disabled via configuration, skipping");
            return;
        }

        _logger.LogInformation("[PlenaryWatcher] Started — polling every {Interval}s",
            _options.PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAndEvaluateAsync(stoppingToken);
            await Task.Delay(_options.PollingInterval, stoppingToken).ContinueWith(_ => { });
        }

        _logger.LogInformation("[PlenaryWatcher] Stopped");
    }

    private async Task PollAndEvaluateAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("[PlenaryWatcher] Polling Câmara and Senado...");

            var camaraSessions = await FetchCamaraSessionsAsync();
            var senadoSessions = await FetchSenadoSessionsAsync();
            var allSessions = camaraSessions.Concat(senadoSessions).ToList();

            if (!allSessions.Any())
            {
                _logger.LogDebug("[PlenaryWatcher] No new sessions found");
                return;
            }

            _logger.LogInformation("[PlenaryWatcher] Found {Count} sessions to evaluate", allSessions.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var alertEngine = scope.ServiceProvider.GetRequiredService<IVotingAlertEngine>();
            var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            foreach (var (dto, chamber) in allSessions)
            {
                await ProcessSessionAsync(dto, chamber, db, alertEngine, pushService, ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "[PlenaryWatcher] Error during poll cycle");
        }
    }

    private async Task ProcessSessionAsync(
        PlenarySessionDto dto,
        string chamber,
        ChecaAIDbContext db,
        IVotingAlertEngine alertEngine,
        IPushNotificationService pushService,
        CancellationToken ct)
    {
        // Skip already known sessions that already have an alert
        var alreadyAlerted = await db.VotingAlerts
            .Include(a => a.VotingSession)
            .AnyAsync(a => a.VotingSession.ExternalId == dto.ExternalId, ct);

        if (alreadyAlerted) return;

        // Find or create the VotingSession entity
        var session = await db.VotingSessions
            .Include(v => v.Proposal)
            .FirstOrDefaultAsync(v => v.ExternalId == dto.ExternalId, ct);

        if (session == null)
        {
            // Find or create a placeholder proposal
            var proposal = await db.Proposals
                .FirstOrDefaultAsync(p => p.ExternalId == dto.ProposalExternalId, ct);

            if (proposal == null)
            {
                proposal = new Proposal
                {
                    ExternalId = dto.ProposalExternalId ?? $"unknown-{dto.ExternalId}",
                    Title = dto.Description ?? "Proposta desconhecida",
                    Type = "N/A",
                    Number = "0",
                    Year = DateTime.UtcNow.Year,
                    Chamber = chamber,
                    Description = dto.Description ?? string.Empty,
                    Status = "In Process"
                };
                db.Proposals.Add(proposal);
                await db.SaveChangesAsync(ct);
            }

            session = new VotingSession
            {
                ExternalId = dto.ExternalId,
                ProposalId = proposal.Id,
                Description = dto.Description ?? "Votação sem descrição",
                VotingDate = dto.VotingDate,
                Chamber = chamber,
                Result = dto.Result ?? "Em andamento",
                TotalVotes = dto.TotalVotes,
                VotesYes = dto.VotesYes,
                VotesNo = dto.VotesNo
            };
            db.VotingSessions.Add(session);
            await db.SaveChangesAsync(ct);
        }

        // Evaluate alert score
        var alert = await alertEngine.EvaluateAsync(session);
        if (alert == null)
        {
            _logger.LogDebug("[PlenaryWatcher] Session {Id} score too low, no alert", dto.ExternalId);
            return;
        }

        // Persist alert
        db.VotingAlerts.Add(alert);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[PlenaryWatcher] 🚨 Alert {Level} (score={Score}) for session {Id} in {Chamber}",
            alert.AlertLevel, alert.Score, session.ExternalId, chamber);

        // Send push notification
        var pushed = await pushService.SendAlertAsync(alert, session);
        if (pushed)
        {
            alert.PushSent = true;
            await db.SaveChangesAsync(ct);
        }

        // SignalR notification is sent by the API hub when clients poll /api/alerts
        // (Worker doesn't have direct access to the API hub — decoupled via DB)
    }

    // ── Câmara dos Deputados ──────────────────────────────────────────────────

    private async Task<List<(PlenarySessionDto dto, string chamber)>> FetchCamaraSessionsAsync()
    {
        try
        {
            var since = DateTime.UtcNow.AddHours(-_options.LookbackHours);
            var url = $"{_options.CamaraBaseUrl}/votacoes?dataInicio={since:yyyy-MM-dd}&dataFim={DateTime.UtcNow:yyyy-MM-dd}&ordem=DESC&ordenarPor=dataHoraRegistro";

            var json = await _httpClient.GetStringAsync(url);
            var response = JsonSerializer.Deserialize<CamaraVotacoesResponse>(json, JsonOpts);

            return response?.Dados?
                .Select(v => (new PlenarySessionDto
                {
                    ExternalId = v.Id ?? string.Empty,
                    ProposalExternalId = v.ProposicaoId?.ToString(),
                    Description = v.Descricao ?? v.Ementa ?? "Votação",
                    VotingDate = v.DataHoraRegistro ?? DateTime.UtcNow,
                    TotalVotes = (v.VotosSim ?? 0) + (v.VotosNao ?? 0) + (v.Abstencoes ?? 0),
                    VotesYes = v.VotosSim ?? 0,
                    VotesNo = v.VotosNao ?? 0,
                    Result = v.Aprovacao == 1 ? "Approved" : "Rejected"
                }, "Câmara"))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlenaryWatcher] Failed to fetch Câmara sessions");
            return [];
        }
    }

    // ── Senado Federal ────────────────────────────────────────────────────────

    private async Task<List<(PlenarySessionDto dto, string chamber)>> FetchSenadoSessionsAsync()
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var url = $"{_options.SenadoBaseUrl}/plenario/votacao/lista/{today}";

            using var response = await _httpClient.GetAsync(url);

            // 404 = no sessions today — treat as empty, not an error
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("[PlenaryWatcher] Senado: no sessions today ({Date})", today);
                return [];
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<SenadoVotacaoListaResponse>(json, JsonOpts);

            var votacoes = parsed?.ListaVotacaoPlenario?.Votacoes?.Votacao ?? [];

            return votacoes
                .Select(v => (new PlenarySessionDto
                {
                    ExternalId = v.CodigoSessaoVotacao ?? v.CodigoVotacao ?? string.Empty,
                    ProposalExternalId = v.CodigoMateria,
                    Description = v.DescricaoVotacao ?? v.Ementa ?? "Votação",
                    VotingDate = DateTime.TryParse(v.DataHoraVotacao, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                    Result = v.DescricaoResultado ?? "Em andamento"
                }, "Senado"))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlenaryWatcher] Failed to fetch Senado sessions");
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── DTOs internos (private nested — file-local types can't appear in method signatures) ──

    private sealed class PlenarySessionDto
    {
        public string ExternalId { get; set; } = string.Empty;
        public string? ProposalExternalId { get; set; }
        public string? Description { get; set; }
        public DateTime VotingDate { get; set; }
        public int TotalVotes { get; set; }
        public int VotesYes { get; set; }
        public int VotesNo { get; set; }
        public string? Result { get; set; }
    }
}

file class CamaraVotacoesResponse
{
    [JsonPropertyName("dados")]
    public List<CamaraVotacaoDto>? Dados { get; set; }
}

file class CamaraVotacaoDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }

    [JsonPropertyName("ementa")]
    public string? Ementa { get; set; }

    [JsonPropertyName("dataHoraRegistro")]
    public DateTime? DataHoraRegistro { get; set; }

    [JsonPropertyName("proposicaoId")]
    public int? ProposicaoId { get; set; }

    [JsonPropertyName("aprovacao")]
    public int? Aprovacao { get; set; }

    [JsonPropertyName("votosSim")]
    public int? VotosSim { get; set; }

    [JsonPropertyName("votosNao")]
    public int? VotosNao { get; set; }

    [JsonPropertyName("abstencoes")]
    public int? Abstencoes { get; set; }
}

file class SenadoVotacaoListaResponse
{
    [JsonPropertyName("ListaVotacaoPlenario")]
    public SenadoListaVotacao? ListaVotacaoPlenario { get; set; }
}

file class SenadoListaVotacao
{
    [JsonPropertyName("Votacoes")]
    public SenadoVotacoes? Votacoes { get; set; }
}

file class SenadoVotacoes
{
    [JsonPropertyName("Votacao")]
    public List<SenadoVotacaoDto>? Votacao { get; set; }
}

file class SenadoVotacaoDto
{
    [JsonPropertyName("CodigoSessaoVotacao")]
    public string? CodigoSessaoVotacao { get; set; }

    [JsonPropertyName("CodigoVotacao")]
    public string? CodigoVotacao { get; set; }

    [JsonPropertyName("CodigoMateria")]
    public string? CodigoMateria { get; set; }

    [JsonPropertyName("DescricaoVotacao")]
    public string? DescricaoVotacao { get; set; }

    [JsonPropertyName("Ementa")]
    public string? Ementa { get; set; }

    [JsonPropertyName("DataHoraVotacao")]
    public string? DataHoraVotacao { get; set; }

    [JsonPropertyName("DescricaoResultado")]
    public string? DescricaoResultado { get; set; }
}

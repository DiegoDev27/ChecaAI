using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Syncs Proposals, VotingSessions, and individual Votes (nominal votes) from:
///   Câmara: /votacoes (last 12 months) + /votacoes/{id}/votos (nominal sessions only)
///   Senado: /plenario/votacao/lista/{date} for recent months + matérias atuais
/// Note: PlenaryWatcherService handles real-time polling (today).
///       This service handles backfill (historical sync).
/// Runs once at startup (after 15min delay), then every 6h.
/// </summary>
public class VoteProposalSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VoteProposalSyncService> _logger;

    private const string CamaraBaseUrl = "https://dadosabertos.camara.leg.br/api/v2";
    private const string SenadoBaseUrl = "https://legis.senado.leg.br/dadosabertos";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(300);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public VoteProposalSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<VoteProposalSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[VoteProposalSync] Service started — first run in {Delay}min", StartupDelay.TotalMinutes);
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VoteProposalSync] Unexpected error during sync");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("StateDeputy");

        _logger.LogInformation("[VoteProposalSync] Starting sync...");

        // Build politician lookup tables
        var deputadoToId = await BuildDeputadoLookupAsync(ct);
        var senatorToId = await BuildSenatorLookupAsync(ct);

        // Sync Câmara proposals (recent 12 months)
        await SyncCamaraProposalsAsync(client, ct);

        // Sync Câmara voting sessions + nominal votes (recent 12 months)
        await SyncCamaraVotacoesAsync(client, deputadoToId, ct);

        // Sync Senado voting sessions (recent 3 months — no individual votes yet)
        await SyncSenadoVotacoesAsync(client, senatorToId, ct);

        _logger.LogInformation("[VoteProposalSync] Sync cycle complete");
    }

    // ── Câmara Proposals ──────────────────────────────────────────────────────

    private async Task SyncCamaraProposalsAsync(HttpClient client, CancellationToken ct)
    {
        var startDate = DateTime.UtcNow.AddMonths(-12).ToString("yyyy-MM-dd");
        _logger.LogInformation("[VoteProposalSync] Fetching Câmara proposals since {Start}...", startDate);

        var page = 1;
        var added = 0;
        var updated = 0;

        while (true)
        {
            if (ct.IsCancellationRequested) break;

            // ordenarPor=dataApresentacao is no longer accepted by the Câmara API (HTTP 400,
            // "Parâmetro(s) inválido(s)"). ordenarPor=id sorts newest-first equivalently here
            // since dataApresentacaoInicio already scopes the window and ids are assigned in order.
            var url = $"{CamaraBaseUrl}/proposicoes?dataApresentacaoInicio={startDate}&pagina={page}&itens=100&ordem=DESC&ordenarPor=id";
            var result = await FetchJsonAsync<CamaraPagedResponse<CamaraProposicaoDto>>(client, url, ct);

            if (result?.Dados == null || result.Dados.Count == 0) break;

            foreach (var dto in result.Dados)
            {
                if (ct.IsCancellationRequested) break;
                var (a, u) = await UpsertProposalAsync(dto, "Câmara", ct);
                added += a; updated += u;
            }

            var hasNext = result.Links?.Any(l => string.Equals(l.Rel, "next", StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!hasNext) break;
            page++;
            await Task.Delay(RateLimitDelay, ct);
        }

        _logger.LogInformation("[VoteProposalSync] Câmara proposals: +{Added} added, {Updated} updated", added, updated);
    }

    private async Task<(int Added, int Updated)> UpsertProposalAsync(
        CamaraProposicaoDto dto, string chamber, CancellationToken ct)
    {
        var externalId = dto.Id.ToString();
        if (externalId.Length > 20) externalId = externalId[..20];

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var existing = await db.Proposals
            .FirstOrDefaultAsync(p => p.ExternalId == externalId, ct);

        var title = (dto.Ementa ?? $"{dto.SiglaTipo} {dto.Numero}/{dto.Ano}");
        title = title.Length > 200 ? title[..200] : title;
        var summary = dto.Ementa?.Length > 500 ? dto.Ementa[..500] : dto.Ementa;
        var status = (dto.StatusProposicao?.DescricaoSituacao ?? "Em andamento").Length > 50
            ? (dto.StatusProposicao?.DescricaoSituacao ?? "Em andamento")[..50]
            : (dto.StatusProposicao?.DescricaoSituacao ?? "Em andamento");

        var now = DateTime.UtcNow;

        if (existing != null)
        {
            existing.Status = status;
            existing.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return (0, 1);
        }

        db.Proposals.Add(new Proposal
        {
            ExternalId = externalId,
            Title = title,
            Description = summary,
            Type = dto.SiglaTipo ?? "PL",
            Number = dto.Numero?.ToString() ?? "0",
            Year = dto.Ano ?? DateTime.UtcNow.Year,
            Chamber = chamber,
            Author = dto.StatusProposicao?.NomeRelator?.Length > 100
                ? dto.StatusProposicao.NomeRelator[..100]
                : dto.StatusProposicao?.NomeRelator,
            Summary = summary,
            Status = status,
            ProposalDate = dto.DataApresentacao,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync(ct);
        return (1, 0);
    }

    // ── Câmara Voting Sessions + Individual Votes ────────────────────────────

    private async Task SyncCamaraVotacoesAsync(
        HttpClient client,
        Dictionary<string, int> deputadoToId,
        CancellationToken ct)
    {
        var startDate = DateTime.UtcNow.AddMonths(-12).ToString("yyyy-MM-dd");
        _logger.LogInformation("[VoteProposalSync] Fetching Câmara votações since {Start}...", startDate);

        var page = 1;
        var sessionsAdded = 0;
        var votesAdded = 0;

        while (true)
        {
            if (ct.IsCancellationRequested) break;

            var url = $"{CamaraBaseUrl}/votacoes?dataInicio={startDate}&pagina={page}&itens=100&ordem=DESC&ordenarPor=dataHoraRegistro";
            var result = await FetchJsonAsync<CamaraPagedResponse<CamaraVotacaoDto>>(client, url, ct);

            if (result?.Dados == null || result.Dados.Count == 0) break;

            foreach (var dto in result.Dados)
            {
                if (ct.IsCancellationRequested) break;

                var (sessionId, isNew) = await UpsertVotingSessionAsync(dto, "Câmara", ct);
                if (sessionId <= 0) continue;
                if (isNew) sessionsAdded++;

                // Skip if no politicians to link votes to
                if (deputadoToId.Count == 0) continue;

                // Skip sessions that already have votes persisted
                var alreadyHasVotes = await CheckSessionHasVotesAsync(sessionId, ct);
                if (alreadyHasVotes) continue;

                // NOTE: tipoVotacao is always empty in the Câmara API list endpoint.
                // We try fetching votes for every session — the /votos endpoint returns
                // an empty array for symbolic votes and individual records for nominal votes.
                var votes = await FetchCamaraVotesAsync(client, dto.Id ?? "", ct);
                if (votes.Count > 0)
                {
                    var vAdded = await PersistVotesAsync(sessionId, votes, deputadoToId, ct);
                    votesAdded += vAdded;
                }

                await Task.Delay(RateLimitDelay, ct);
            }

            var hasNext = result.Links?.Any(l => string.Equals(l.Rel, "next", StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!hasNext) break;
            page++;
            await Task.Delay(RateLimitDelay, ct);
        }

        _logger.LogInformation("[VoteProposalSync] Câmara: +{Sessions} sessions, +{Votes} nominal votes", sessionsAdded, votesAdded);
    }

    private async Task<(int SessionId, bool IsNew)> UpsertVotingSessionAsync(
        CamaraVotacaoDto dto, string chamber, CancellationToken ct)
    {
        var externalId = dto.Id ?? string.Empty;
        if (string.IsNullOrWhiteSpace(externalId)) return (0, false);
        if (externalId.Length > 50) externalId = externalId[..50];

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var existing = await db.VotingSessions
            .FirstOrDefaultAsync(v => v.ExternalId == externalId, ct);

        if (existing != null)
            return (existing.Id, false);

        // Find or create the referenced proposal
        var proposalExtId = dto.ProposicaoId?.ToString() ?? $"unknown-{externalId}";
        if (proposalExtId.Length > 20) proposalExtId = proposalExtId[..20];

        var proposal = await db.Proposals.FirstOrDefaultAsync(p => p.ExternalId == proposalExtId, ct);
        if (proposal == null)
        {
            var now2 = DateTime.UtcNow;
            proposal = new Proposal
            {
                ExternalId = proposalExtId,
                Title = (dto.Descricao ?? dto.Ementa ?? "Proposta")
                    .Length > 200 ? (dto.Descricao ?? dto.Ementa ?? "Proposta")[..200] : (dto.Descricao ?? dto.Ementa ?? "Proposta"),
                Type = "N/A",
                Number = "0",
                Year = dto.DataHoraRegistro?.Year ?? DateTime.UtcNow.Year,
                Chamber = chamber,
                Status = "Em andamento",
                CreatedAt = now2,
                UpdatedAt = now2
            };
            db.Proposals.Add(proposal);
            await db.SaveChangesAsync(ct);
        }

        var desc = (dto.Descricao ?? dto.Ementa ?? "Votação");
        var totalVotes = (dto.VotosSim ?? 0) + (dto.VotosNao ?? 0) + (dto.Abstencoes ?? 0);
        var now = DateTime.UtcNow;

        var session = new VotingSession
        {
            ExternalId = externalId,
            ProposalId = proposal.Id,
            Description = desc.Length > 200 ? desc[..200] : desc,
            VotingDate = dto.DataHoraRegistro?.ToUniversalTime() ?? now,
            SessionType = dto.TipoVotacao,
            Chamber = chamber,
            Result = (dto.Aprovacao == 1 ? "Approved" : dto.Aprovacao == 0 ? "Rejected" : "Em andamento"),
            TotalVotes = totalVotes,
            VotesYes = dto.VotosSim ?? 0,
            VotesNo = dto.VotosNao ?? 0,
            VotesAbstention = dto.Abstencoes ?? 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.VotingSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return (session.Id, true);
    }

    private async Task<List<CamaraVotoDto>> FetchCamaraVotesAsync(
        HttpClient client, string votacaoId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(votacaoId)) return [];

        var url = $"{CamaraBaseUrl}/votacoes/{votacaoId}/votos";
        var result = await FetchJsonAsync<CamaraPagedResponse<CamaraVotoDto>>(client, url, ct);
        return result?.Dados ?? [];
    }

    private async Task<bool> CheckSessionHasVotesAsync(int sessionId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
        return await db.Votes.AnyAsync(v => v.VotingSessionId == sessionId, ct);
    }

    private async Task<int> PersistVotesAsync(
        int sessionId,
        List<CamaraVotoDto> votes,
        Dictionary<string, int> deputadoToId,
        CancellationToken ct)
    {
        if (votes.Count == 0) return 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Load existing politician IDs for this session
        var existingPoliticianIds = (await db.Votes
            .Where(v => v.VotingSessionId == sessionId)
            .Select(v => v.PoliticianId)
            .ToListAsync(ct))
            .ToHashSet();

        var added = 0;
        var now = DateTime.UtcNow;

        foreach (var voto in votes)
        {
            var depId = voto.Deputado?.Id.ToString() ?? "";
            if (!deputadoToId.TryGetValue(depId, out var politicianId))
                continue;

            if (existingPoliticianIds.Contains(politicianId))
                continue;

            var voteValue = MapCamaraVoteValue(voto.TipoVoto);

            db.Votes.Add(new Vote
            {
                PoliticianId = politicianId,
                VotingSessionId = sessionId,
                VoteValue = voteValue,
                CreatedAt = now
            });

            existingPoliticianIds.Add(politicianId);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return added;
    }

    // ── Senado Voting Sessions ────────────────────────────────────────────────

    private async Task SyncSenadoVotacoesAsync(
        HttpClient client,
        Dictionary<string, int> senatorToId,
        CancellationToken ct)
    {
        _logger.LogInformation("[VoteProposalSync] Fetching Senado voting sessions (last 3 months)...");

        var months = Enumerable.Range(0, 3)
            .Select(i => DateTime.UtcNow.AddMonths(-i))
            .ToList();

        var sessionsAdded = 0;

        foreach (var month in months)
        {
            if (ct.IsCancellationRequested) break;

            var urls = new[]
            {
                $"{SenadoBaseUrl}/plenario/votacao/lista/{month:yyyyMMdd}",
                $"{SenadoBaseUrl}/plenario/listaVotacoes?data={month:yyyyMMdd}",
                $"{SenadoBaseUrl}/plenario/listaVotacoes?ano={month.Year}&mes={month.Month:D2}"
            };

            var fetchedThisMonth = false;
            foreach (var tryUrl in urls)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var response = await client.GetAsync(tryUrl, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("[VoteProposalSync] Senado URL HTTP {Status}: {Url}", (int)response.StatusCode, tryUrl);
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogDebug("[VoteProposalSync] Senado response ({Bytes}b) preview: {Preview}",
                        json.Length, json.Length > 200 ? json[..200] : json);
                    var added = await ParseAndPersistSenadoVotacoesAsync(json, ct);
                    sessionsAdded += added;
                    fetchedThisMonth = true;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[VoteProposalSync] Exception on Senado URL: {Url}", tryUrl);
                }
            }

            if (!fetchedThisMonth)
                _logger.LogWarning("[VoteProposalSync] All Senado URLs failed for {Month:yyyy-MM}", month);

            await Task.Delay(RateLimitDelay, ct);
        }

        _logger.LogInformation("[VoteProposalSync] Senado: +{Sessions} voting sessions", sessionsAdded);
    }

    private async Task<int> ParseAndPersistSenadoVotacoesAsync(string json, CancellationToken ct)
    {
        var added = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Navigate different possible JSON structures
            JsonElement? votacoesEl = null;
            if (root.TryGetProperty("ListaVotacaoPlenario", out var lvp) &&
                lvp.TryGetProperty("Votacoes", out var vots) &&
                vots.TryGetProperty("Votacao", out var v))
                votacoesEl = v;
            else if (root.TryGetProperty("VotacoesPlenario", out var vp) &&
                     vp.TryGetProperty("Votacao", out var v2))
                votacoesEl = v2;

            if (votacoesEl == null) return 0;

            var now = DateTime.UtcNow;

            async Task ProcessVotacaoEl(JsonElement el)
            {
                var extId = GetStr(el, "CodigoSessaoVotacao") ?? GetStr(el, "CodigoVotacao");
                if (string.IsNullOrWhiteSpace(extId)) return;
                if (extId.Length > 50) extId = extId[..50];

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

                if (await db.VotingSessions.AnyAsync(v => v.ExternalId == extId, ct)) return;

                var propExtId = GetStr(el, "CodigoMateria") ?? $"senado-unknown-{extId}";
                if (propExtId.Length > 20) propExtId = propExtId[..20];

                var proposal = await db.Proposals.FirstOrDefaultAsync(p => p.ExternalId == propExtId, ct);
                if (proposal == null)
                {
                    proposal = new Proposal
                    {
                        ExternalId = propExtId,
                        Title = (GetStr(el, "DescricaoVotacao") ?? GetStr(el, "Ementa") ?? "Matéria Senado")
                            .Length > 200 ? (GetStr(el, "DescricaoVotacao") ?? "Matéria Senado")[..200] : (GetStr(el, "DescricaoVotacao") ?? "Matéria Senado"),
                        Type = "N/A",
                        Number = "0",
                        Year = DateTime.UtcNow.Year,
                        Chamber = "Senado",
                        Status = "Em andamento",
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    db.Proposals.Add(proposal);
                    await db.SaveChangesAsync(ct);
                }

                var dateStr = GetStr(el, "DataHoraVotacao");
                var votingDate = DateTime.TryParse(dateStr, out var dt) ? dt.ToUniversalTime() : now;
                var desc = GetStr(el, "DescricaoVotacao") ?? GetStr(el, "Ementa") ?? "Votação";
                var resultado = GetStr(el, "DescricaoResultado") ?? "Em andamento";

                var session = new VotingSession
                {
                    ExternalId = extId,
                    ProposalId = proposal.Id,
                    Description = desc.Length > 200 ? desc[..200] : desc,
                    VotingDate = votingDate,
                    Chamber = "Senado",
                    Result = resultado.Length > 20 ? resultado[..20] : resultado,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.VotingSessions.Add(session);
                await db.SaveChangesAsync(ct);
                added++;
            }

            if (votacoesEl.Value.ValueKind == JsonValueKind.Array)
                foreach (var el in votacoesEl.Value.EnumerateArray())
                    await ProcessVotacaoEl(el);
            else if (votacoesEl.Value.ValueKind == JsonValueKind.Object)
                await ProcessVotacaoEl(votacoesEl.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[VoteProposalSync] Failed to parse Senado votações JSON");
        }

        return added;
    }

    private static string? GetStr(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // ── Shared helpers ────────────────────────────────────────────────────────

    private async Task<Dictionary<string, int>> BuildDeputadoLookupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
        var list = await db.Politicians
            .Where(p => p.PoliticalPosition == "Federal Deputy" && p.ExternalId != null)
            .Select(p => new { p.ExternalId, p.Id })
            .AsNoTracking()
            .ToListAsync(ct);
        return list.Where(p => p.ExternalId != null)
            .ToDictionary(p => p.ExternalId!, p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, int>> BuildSenatorLookupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
        var list = await db.Politicians
            .Where(p => p.PoliticalPosition == "Senator" && p.ExternalId != null)
            .Select(p => new { p.ExternalId, p.Id })
            .AsNoTracking()
            .ToListAsync(ct);
        return list.Where(p => p.ExternalId != null)
            .ToDictionary(p => p.ExternalId!, p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    // Retries transient failures (network errors, 5xx, 429) so a single hiccup mid-backfill
    // doesn't get misread by callers as "no more pages" and silently truncate the sync.
    private async Task<T?> FetchJsonAsync<T>(HttpClient client, string url, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.GetAsync(url, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return default;

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    return JsonSerializer.Deserialize<T>(json, JsonOpts);
                }

                if (attempt == maxAttempts)
                {
                    _logger.LogWarning("[VoteProposalSync] Giving up after {Attempts} attempts, HTTP {Status}: {Url}",
                        maxAttempts, response.StatusCode, url);
                    return default;
                }

                _logger.LogWarning("[VoteProposalSync] HTTP {Status} on attempt {Attempt}/{Max}, retrying: {Url}",
                    response.StatusCode, attempt, maxAttempts, url);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "[VoteProposalSync] Fetch failed on attempt {Attempt}/{Max}, retrying: {Url}",
                    attempt, maxAttempts, url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VoteProposalSync] Giving up after {Attempts} attempts: {Url}", maxAttempts, url);
                return default;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
            catch (OperationCanceledException)
            {
                // Must not escape uncaught — the outer ExecuteAsync loop treats any
                // OperationCanceledException seen while stoppingToken.IsCancellationRequested
                // as a deliberate shutdown and permanently stops this service's periodic sync.
                return default;
            }
        }

        return default;
    }

    private static string MapCamaraVoteValue(string? tipoVoto) =>
        tipoVoto?.ToUpperInvariant() switch
        {
            "SIM" => "Yes",
            "NÃO" or "NAO" or "NÃO" => "No",
            "ABSTENÇÃO" or "ABSTENCAO" or "ABSTENÇÃO" => "Abstention",
            "ARTIGO 17" or "OBSTRUÇÃO" or "OBSTRUCAO" => "Abstention",
            _ => "Absent"
        };

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class CamaraPagedResponse<T>
    {
        [JsonPropertyName("dados")]
        public List<T> Dados { get; set; } = new();
        [JsonPropertyName("links")]
        public List<CamaraLink>? Links { get; set; }
    }

    private sealed class CamaraLink
    {
        [JsonPropertyName("rel")]
        public string Rel { get; set; } = string.Empty;
    }

    private sealed class CamaraProposicaoDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("siglaTipo")]
        public string? SiglaTipo { get; set; }

        [JsonPropertyName("numero")]
        public int? Numero { get; set; }

        [JsonPropertyName("ano")]
        public int? Ano { get; set; }

        [JsonPropertyName("ementa")]
        public string? Ementa { get; set; }

        [JsonPropertyName("dataApresentacao")]
        public DateTime? DataApresentacao { get; set; }

        [JsonPropertyName("statusProposicao")]
        public CamaraStatusProposicaoDto? StatusProposicao { get; set; }
    }

    private sealed class CamaraStatusProposicaoDto
    {
        [JsonPropertyName("descricaoSituacao")]
        public string? DescricaoSituacao { get; set; }

        [JsonPropertyName("nomeRelator")]
        public string? NomeRelator { get; set; }
    }

    private sealed class CamaraVotacaoDto
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

        [JsonPropertyName("tipoVotacao")]
        public string? TipoVotacao { get; set; }

        [JsonPropertyName("votosSim")]
        public int? VotosSim { get; set; }

        [JsonPropertyName("votosNao")]
        public int? VotosNao { get; set; }

        [JsonPropertyName("abstencoes")]
        public int? Abstencoes { get; set; }
    }

    private sealed class CamaraVotoDto
    {
        [JsonPropertyName("deputado_")]
        public CamaraDeputadoRefDto? Deputado { get; set; }

        [JsonPropertyName("tipoVoto")]
        public string? TipoVoto { get; set; }
    }

    private sealed class CamaraDeputadoRefDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}

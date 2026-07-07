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
/// Syncs parliamentary committees (Committee) and their memberships (CommitteeMembership).
/// Sources:
///   Câmara: GET /orgaos (all organs/committees) + /orgaos/{id}/membros
///   Senado: GET /comissao/lista.json?tipo={tipo} + /comissao/{sigla}/membros.json
/// Runs once at startup (after 12min delay), then every 24h.
/// </summary>
public class CommitteeSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CommitteeSyncService> _logger;

    private const string CamaraBaseUrl = "https://dadosabertos.camara.leg.br/api/v2";
    private const string SenadoBaseUrl = "https://legis.senado.leg.br/dadosabertos";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(200);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    // Câmara codTipoOrgao values that represent real committees (numeric — more reliable than string tipoOrgao)
    // 2 = Comissão Permanente, 3 = Comissão Especial, 4 = CPI, 5 = Comissão Externa, 6 = Comissão Mista Permanente
    // 7 = Comissão Parlamentar Mista de Inquérito, 10 = Comissão Temporária
    private static readonly HashSet<int> RelevantCommitteeTypeCodes = new()
    {
        2, 3, 4, 5, 6, 7, 10
    };

    public CommitteeSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<CommitteeSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CommitteeSync] Service started — first run in {Delay}min", StartupDelay.TotalMinutes);
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncCommitteesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CommitteeSync] Unexpected error during sync");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncCommitteesAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("StateDeputy");

        _logger.LogInformation("[CommitteeSync] Starting committee sync (Câmara + Senado)...");

        var camaraAdded = await SyncCamaraCommitteesAsync(client, ct);
        var senadoAdded = await SyncSenadoCommitteesAsync(client, ct);

        _logger.LogInformation("[CommitteeSync] Done — Câmara: {Camara} committees, Senado: {Senado} committees",
            camaraAdded, senadoAdded);
    }

    // ── Câmara ────────────────────────────────────────────────────────────────

    private async Task<int> SyncCamaraCommitteesAsync(HttpClient client, CancellationToken ct)
    {
        _logger.LogInformation("[CommitteeSync] Fetching Câmara committees from /orgaos...");

        var organs = await FetchAllCamaraOrgaosAsync(client, ct);

        // Log actual tipoOrgao / codTipoOrgao values for debugging (first few organs)
        var typeGroups = organs.GroupBy(o => o.CodTipoOrgao).OrderBy(g => g.Key).Take(10);
        foreach (var g in typeGroups)
            _logger.LogDebug("[CommitteeSync] codTipoOrgao={Code} tipoOrgao='{Type}' count={Count}",
                g.Key, g.First().TipoOrgao, g.Count());

        var committees = organs
            .Where(o => RelevantCommitteeTypeCodes.Contains(o.CodTipoOrgao))
            .ToList();

        _logger.LogInformation("[CommitteeSync] Found {Count} relevant committees in Câmara (of {Total} organs)",
            committees.Count, organs.Count);

        // Build politician lookup: Câmara deputado ID → our PoliticianId
        var deputadoToId = await BuildCamaraDeputadoLookupAsync(ct);

        var added = 0;
        var membersSynced = 0;

        foreach (var organ in committees)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var committeeId = await UpsertCommitteeAsync(
                    externalId: $"camara-{organ.Id}",
                    name: organ.Nome,
                    acronym: organ.Sigla,
                    committeeType: MapCamaraType(organ.TipoOrgao, organ.CodTipoOrgao),
                    chamber: "Câmara",
                    isActive: true,
                    ct);

                if (committeeId > 0) added++;

                // Fetch members
                var members = await FetchCamaraMembersAsync(client, organ.Id.ToString(), ct);
                if (members.Count > 0)
                {
                    var synced = await UpsertMembershipsAsync(committeeId, members, deputadoToId, ct);
                    membersSynced += synced;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CommitteeSync] Failed to sync Câmara committee {Sigla}", organ.Sigla);
            }

            await Task.Delay(RateLimitDelay, ct);
        }

        _logger.LogInformation("[CommitteeSync] Câmara: processed {Committees} committees, {Members} memberships",
            added, membersSynced);
        return added;
    }

    private async Task<List<CamaraOrgaoDto>> FetchAllCamaraOrgaosAsync(HttpClient client, CancellationToken ct)
    {
        var all = new List<CamaraOrgaoDto>();
        var page = 1;

        while (true)
        {
            if (ct.IsCancellationRequested) break;

            var url = $"{CamaraBaseUrl}/orgaos?pagina={page}&itens=100&ordem=ASC&ordenarPor=sigla";
            HttpResponseMessage response;
            try { response = await client.GetAsync(url, ct); }
            catch { break; }

            if (!response.IsSuccessStatusCode) break;

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<CamaraPagedResponse<CamaraOrgaoDto>>(json, JsonOpts);

            if (result?.Dados == null || result.Dados.Count == 0) break;
            all.AddRange(result.Dados);

            var hasNext = result.Links?.Any(l => string.Equals(l.Rel, "next", StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!hasNext) break;
            page++;

            await Task.Delay(RateLimitDelay, ct);
        }

        return all;
    }

    private async Task<List<CamaraMemberDto>> FetchCamaraMembersAsync(
        HttpClient client, string orgaoId, CancellationToken ct)
    {
        // idLegislatura=58 is the current legislature (started Feb 2023)
        var url = $"{CamaraBaseUrl}/orgaos/{orgaoId}/membros?idLegislatura=58";
        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("[CommitteeSync] /membros HTTP {Status} for orgao {Id}", (int)response.StatusCode, orgaoId);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<CamaraPagedResponse<CamaraMemberDto>>(json, JsonOpts);
            return result?.Dados ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[CommitteeSync] Exception fetching /membros for orgao {Id}", orgaoId);
            return [];
        }
    }

    private async Task<Dictionary<string, int>> BuildCamaraDeputadoLookupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var list = await db.Politicians
            .Where(p => p.PoliticalPosition == "Federal Deputy" && p.ExternalId != null)
            .Select(p => new { p.ExternalId, p.Id })
            .AsNoTracking()
            .ToListAsync(ct);

        return list
            .Where(p => p.ExternalId != null)
            .ToDictionary(p => p.ExternalId!, p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<int> UpsertMembershipsAsync(
        int committeeId,
        List<CamaraMemberDto> members,
        Dictionary<string, int> deputadoToId,
        CancellationToken ct)
    {
        if (committeeId <= 0) return 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Load existing memberships for this committee
        var existing = await db.CommitteeMemberships
            .Where(m => m.CommitteeId == committeeId)
            .Select(m => m.PoliticianId)
            .ToHashSetAsync(ct);

        var added = 0;
        var now = DateTime.UtcNow;

        foreach (var member in members)
        {
            var deputadoIdStr = member.Id.ToString();
            if (!deputadoToId.TryGetValue(deputadoIdStr, out var politicianId))
                continue;

            if (existing.Contains(politicianId))
                continue;

            DateTime? startDate = null;
            DateTime? endDate = null;
            if (DateTime.TryParse(member.DataInicio, out var sd)) startDate = DateTime.SpecifyKind(sd, DateTimeKind.Utc);
            if (DateTime.TryParse(member.DataFim, out var ed)) endDate = DateTime.SpecifyKind(ed, DateTimeKind.Utc);

            db.CommitteeMemberships.Add(new CommitteeMembership
            {
                CommitteeId = committeeId,
                PoliticianId = politicianId,
                Role = MapCamaraRole(member.Titulo),
                StartDate = startDate,
                EndDate = endDate,
                CreatedAt = now
            });

            existing.Add(politicianId);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return added;
    }

    // ── Senado ────────────────────────────────────────────────────────────────

    // Only these substrings of DescricaoTipoColegiado count as a "committee" for our domain.
    // The Senado's /comissao/lista/colegiados endpoint returns 200+ collegiate bodies, most of
    // which are Grupos Parlamentares (friendship groups), Frentes Parlamentares, Conselhos,
    // Mesa, Plenário, etc. — not committees.
    private static readonly string[] RelevantSenadoTypeKeywords =
        ["COMISSÃO", "COMISSAO", "SUBCOMISSÃO", "SUBCOMISSAO", "CPI", "COMITÊ", "COMITE"];

    private async Task<int> SyncSenadoCommitteesAsync(HttpClient client, CancellationToken ct)
    {
        _logger.LogInformation("[CommitteeSync] Fetching Senado committees...");

        // Build senator lookup: Senate ExternalId → our PoliticianId
        var senatorToId = await BuildSenatorLookupAsync(ct);

        var allColegiados = await FetchSenadoColegiadosAsync(client, ct);
        var committees = allColegiados
            .Where(c => !string.IsNullOrWhiteSpace(c.Codigo) && !string.IsNullOrWhiteSpace(c.Sigla))
            .Where(c => RelevantSenadoTypeKeywords.Any(k =>
                (c.DescricaoTipoColegiado ?? "").Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        _logger.LogInformation("[CommitteeSync] Found {Count} relevant committees in Senado (of {Total} colegiados)",
            committees.Count, allColegiados.Count);

        var added = 0;
        var membersSynced = 0;

        foreach (var comm in committees)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var committeeId = await UpsertCommitteeAsync(
                    externalId: $"senado-{comm.Sigla}",
                    name: comm.Nome ?? comm.Sigla!,
                    acronym: comm.Sigla,
                    committeeType: MapSenadoType(comm.DescricaoTipoColegiado),
                    chamber: comm.SiglaCasa == "CN" ? "Mista" : "Senado",
                    isActive: true,
                    ct);

                if (committeeId > 0) added++;

                var members = await FetchSenadoMembersAsync(client, comm.Codigo!, ct);
                if (members.Count > 0)
                {
                    var synced = await UpsertSenadoMembershipsAsync(committeeId, members, senatorToId, ct);
                    membersSynced += synced;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CommitteeSync] Failed to sync Senado committee {Sigla}", comm.Sigla);
            }

            await Task.Delay(RateLimitDelay, ct);
        }

        _logger.LogInformation("[CommitteeSync] Senado: processed {Committees} committees, {Members} memberships",
            added, membersSynced);
        return added;
    }

    private async Task<List<SenadoColegiadoDto>> FetchSenadoColegiadosAsync(HttpClient client, CancellationToken ct)
    {
        // Senado restructured their API — the old /comissao/lista.json?tipo=X is gone.
        // Current endpoint: /comissao/lista/colegiados (single call, returns every collegiate
        // body of the Congresso Nacional, filtered client-side by DescricaoTipoColegiado).
        var url = $"{SenadoBaseUrl}/comissao/lista/colegiados";
        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CommitteeSync] Senado colegiados list HTTP {Status} url={Url}", (int)response.StatusCode, url);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Navigate: ListaColegiados.Colegiados.Colegiado[...]
            if (!root.TryGetProperty("ListaColegiados", out var lista)) return [];
            if (!lista.TryGetProperty("Colegiados", out var colegiados)) return [];
            if (!colegiados.TryGetProperty("Colegiado", out var colegiadoEl)) return [];

            var result = new List<SenadoColegiadoDto>();

            void ParseColegiado(JsonElement el)
            {
                string? Get(string prop) => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                var codigo = Get("Codigo");
                var sigla = Get("Sigla");
                if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(sigla)) return;

                result.Add(new SenadoColegiadoDto
                {
                    Codigo = codigo,
                    Sigla = sigla,
                    Nome = Get("Nome"),
                    DescricaoTipoColegiado = Get("DescricaoTipoColegiado"),
                    SiglaCasa = Get("SiglaCasa"),
                });
            }

            if (colegiadoEl.ValueKind == JsonValueKind.Array)
                foreach (var el in colegiadoEl.EnumerateArray()) ParseColegiado(el);
            else if (colegiadoEl.ValueKind == JsonValueKind.Object)
                ParseColegiado(colegiadoEl);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CommitteeSync] Failed to fetch Senado colegiados");
            return [];
        }
    }

    private async Task<List<SenadoMemberDto>> FetchSenadoMembersAsync(
        HttpClient client, string codigoComissao, CancellationToken ct)
    {
        // Old endpoint /comissao/{sigla}/membros.json is gone (404). Current one takes the
        // numeric Codigo (not Sigla) and requires the "ativas" query param.
        var url = $"{SenadoBaseUrl}/composicao/comissao/{codigoComissao}?ativas=S";
        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Navigate: ComposicaoAtivaComissaoSf.ComposicaoComissao.Membros.Membro[...]
            if (!root.TryGetProperty("ComposicaoAtivaComissaoSf", out var comp)) return [];
            if (!comp.TryGetProperty("ComposicaoComissao", out var composicao)) return [];
            if (!composicao.TryGetProperty("Membros", out var membrosEl)) return [];
            if (!membrosEl.TryGetProperty("Membro", out var membroEl)) return [];

            var result = new List<SenadoMemberDto>();

            void ParseMember(JsonElement el)
            {
                var codigo = el.TryGetProperty("CodigoMembro", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                var tipoVaga = el.TryGetProperty("TipoVaga", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

                if (!string.IsNullOrWhiteSpace(codigo))
                    result.Add(new SenadoMemberDto { CodigoParlamentar = codigo, DescricaoCargo = tipoVaga });
            }

            if (membroEl.ValueKind == JsonValueKind.Array)
                foreach (var el in membroEl.EnumerateArray()) ParseMember(el);
            else if (membroEl.ValueKind == JsonValueKind.Object)
                ParseMember(membroEl);

            return result;
        }
        catch
        {
            return [];
        }
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

        return list
            .Where(p => p.ExternalId != null)
            .ToDictionary(p => p.ExternalId!, p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<int> UpsertSenadoMembershipsAsync(
        int committeeId,
        List<SenadoMemberDto> members,
        Dictionary<string, int> senatorToId,
        CancellationToken ct)
    {
        if (committeeId <= 0) return 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var existing = await db.CommitteeMemberships
            .Where(m => m.CommitteeId == committeeId)
            .Select(m => m.PoliticianId)
            .ToHashSetAsync(ct);

        var added = 0;
        var now = DateTime.UtcNow;

        foreach (var member in members)
        {
            if (!senatorToId.TryGetValue(member.CodigoParlamentar, out var politicianId))
                continue;
            if (existing.Contains(politicianId))
                continue;

            db.CommitteeMemberships.Add(new CommitteeMembership
            {
                CommitteeId = committeeId,
                PoliticianId = politicianId,
                Role = MapSenadoRole(member.DescricaoCargo),
                CreatedAt = now
            });

            existing.Add(politicianId);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return added;
    }

    // ── Shared upsert ─────────────────────────────────────────────────────────

    /// <summary>Upserts committee by ExternalId. Returns the committee's internal Id.</summary>
    private async Task<int> UpsertCommitteeAsync(
        string externalId, string name, string? acronym,
        string committeeType, string chamber, bool isActive,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var existing = await db.Committees
            .FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);

        var now = DateTime.UtcNow;

        if (existing != null)
        {
            existing.Name = name.Length > 200 ? name[..200] : name;
            existing.Acronym = acronym?.Length > 20 ? acronym[..20] : acronym;
            existing.CommitteeType = committeeType;
            existing.IsActive = isActive;
            existing.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var committee = new Committee
        {
            ExternalId = externalId.Length > 50 ? externalId[..50] : externalId,
            Name = name.Length > 200 ? name[..200] : name,
            Acronym = acronym?.Length > 20 ? acronym[..20] : acronym,
            CommitteeType = committeeType,
            Chamber = chamber,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Committees.Add(committee);
        await db.SaveChangesAsync(ct);
        return committee.Id;
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static string MapCamaraType(string? tipoOrgao, int codTipoOrgao = 0) => codTipoOrgao switch
    {
        2 => "Permanente",
        3 or 5 or 10 => "Especial",
        4 or 7 => "CPI",
        6 => "Mista",
        _ => tipoOrgao?.ToUpperInvariant() switch
        {
            var t when t?.Contains("PERMANENTE") == true && t.Contains("MISTA") => "Mista",
            var t when t?.Contains("PERMANENTE") == true => "Permanente",
            var t when t?.Contains("CPI") == true || t?.Contains("INQUERITO") == true || t?.Contains("INQUÉRITO") == true => "CPI",
            var t when t?.Contains("MISTA") == true => "Mista",
            _ => "Especial"
        }
    };

    private static string MapSenadoType(string? descricaoTipoColegiado)
    {
        var d = (descricaoTipoColegiado ?? "").ToUpperInvariant();
        if (d.Contains("INQUÉRITO") || d.Contains("INQUERITO") || d.Contains("CPI")) return "CPI";
        if (d.Contains("PERMANENTE")) return "Permanente";
        return "Especial";
    }

    private static string MapCamaraRole(string? titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return "Titular";
        var t = titulo.ToUpperInvariant();
        if (t.Contains("PRESIDENTE")) return "Presidente";
        if (t.Contains("VICE-PRES") || t.Contains("VICE PRES")) return "VicePresidente";
        if (t.Contains("SUPLENTE")) return "Suplente";
        return "Titular";
    }

    private static string MapSenadoRole(string? cargo)
    {
        if (string.IsNullOrWhiteSpace(cargo)) return "Titular";
        var c = cargo.ToUpperInvariant();
        if (c.Contains("PRESIDENTE")) return "Presidente";
        if (c.Contains("VICE")) return "VicePresidente";
        if (c.Contains("SUPLENTE")) return "Suplente";
        return "Titular";
    }

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

    private sealed class CamaraOrgaoDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("sigla")]
        public string? Sigla { get; set; }

        [JsonPropertyName("nome")]
        public string Nome { get; set; } = string.Empty;

        [JsonPropertyName("tipoOrgao")]
        public string? TipoOrgao { get; set; }

        [JsonPropertyName("codTipoOrgao")]
        public int CodTipoOrgao { get; set; }

        [JsonPropertyName("apelido")]
        public string? Apelido { get; set; }
    }

    private sealed class CamaraMemberDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("titulo")]
        public string? Titulo { get; set; }

        [JsonPropertyName("dataInicio")]
        public string? DataInicio { get; set; }

        [JsonPropertyName("dataFim")]
        public string? DataFim { get; set; }
    }

    private sealed class SenadoColegiadoDto
    {
        public string? Codigo { get; set; }
        public string? Sigla { get; set; }
        public string? Nome { get; set; }
        public string? DescricaoTipoColegiado { get; set; }
        public string? SiglaCasa { get; set; }
    }

    private sealed class SenadoMemberDto
    {
        public string CodigoParlamentar { get; set; } = string.Empty;
        public string? DescricaoCargo { get; set; }
    }
}

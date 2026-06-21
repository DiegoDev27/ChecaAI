using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Syncs SessionAttendance from Câmara dos Deputados using the eventos API.
///
/// Correct approach (replaces the broken /frequencias endpoint):
///   Step 1: GET /eventos?dataInicio=...&dataFim=...&pagina={p}&itens=100
///           Filter events where descricaoTipo contains "Sessão" (plenary sessions)
///   Step 2: For each session event: GET /eventos/{id}/deputados
///           Returns all deputies with their situacao (Presente / Ausente / etc.)
///
/// Runs every 12 hours. Covers last 90 days rolling window.
/// Upserts by ExternalId = "{eventId}-{deputyExternalId}" to avoid duplicates.
/// </summary>
public class AttendanceSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AttendanceSyncService> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private const string CamaraBaseUrl = "https://dadosabertos.camara.leg.br/api/v2";

    // Session types that represent plenary attendance
    private static readonly HashSet<string> PlenarySessionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sessão Plenária",
        "Sessão Deliberativa",
        "Sessão Ordinária",
        "Sessão Extraordinária",
        "Sessão Solene",
        "Sessão Especial",
        "Sessão Conjunta",
    };

    private static readonly HashSet<string> PresentLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Presente", "Presença"
    };

    public AttendanceSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<AttendanceSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[AttendanceSync] Starting attendance sync cycle...");

            try
            {
                await SyncAttendanceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[AttendanceSync] Unhandled error during sync cycle");
            }

            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }

    // ── Main sync ─────────────────────────────────────────────────────────────

    private async Task SyncAttendanceAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Chamber");

        // Build a lookup: Câmara ExternalId (numeric string) → DB PoliticianId
        Dictionary<string, int> deputyLookup;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var list = await db.Politicians
                .Where(p => p.PoliticalPosition == "Federal Deputy"
                         && p.IsActive
                         && p.ExternalId != null)
                .Select(p => new { p.Id, p.ExternalId })
                .AsNoTracking()
                .ToListAsync(ct);

            deputyLookup = list.ToDictionary(
                p => p.ExternalId!,
                p => p.Id,
                StringComparer.OrdinalIgnoreCase);
        }

        if (deputyLookup.Count == 0)
        {
            _logger.LogWarning("[AttendanceSync] No federal deputies in DB — skipping");
            return;
        }

        _logger.LogInformation("[AttendanceSync] Loaded {Count} federal deputies for matching", deputyLookup.Count);

        // Rolling window: last 90 days
        var dateEnd = DateTime.UtcNow.Date;
        var dateStart = dateEnd.AddDays(-90);

        // Step 1: fetch all plenary session events in the window
        var sessions = await FetchPlenarySessionsAsync(client, dateStart, dateEnd, ct);
        _logger.LogInformation("[AttendanceSync] Found {Count} plenary sessions between {Start:yyyy-MM-dd} and {End:yyyy-MM-dd}",
            sessions.Count, dateStart, dateEnd);

        if (sessions.Count == 0) return;

        // Load already-processed event IDs to skip
        HashSet<string> processedEventIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var ids = await db.SessionAttendances
                .Where(a => a.ExternalId != null && a.Chamber == "Câmara")
                .Select(a => a.ExternalId!)
                .Distinct()
                .ToListAsync(ct);

            // ExternalIds are "{eventId}-{deputyId}" — extract the event part
            processedEventIds = ids
                .Select(id => id.Split('-')[0])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var totalAdded = 0;

        foreach (var session in sessions)
        {
            if (ct.IsCancellationRequested) break;

            var eventIdStr = session.Id.ToString();
            if (processedEventIds.Contains(eventIdStr))
            {
                _logger.LogDebug("[AttendanceSync] Event {Id} already processed — skipping", session.Id);
                continue;
            }

            var added = await SyncEventAttendanceAsync(client, session, deputyLookup, ct);
            totalAdded += added;

            await Task.Delay(200, ct); // rate limit
        }

        _logger.LogInformation("[AttendanceSync] Sync complete — {Added} new attendance records", totalAdded);
    }

    // ── Step 1: fetch plenary sessions ────────────────────────────────────────

    private async Task<List<CamaraEventSummary>> FetchPlenarySessionsAsync(
        HttpClient client, DateTime dateStart, DateTime dateEnd, CancellationToken ct)
    {
        var results = new List<CamaraEventSummary>();
        var page = 1;

        while (!ct.IsCancellationRequested)
        {
            var url = $"{CamaraBaseUrl}/eventos" +
                      $"?dataInicio={dateStart:yyyy-MM-dd}&dataFim={dateEnd:yyyy-MM-dd}" +
                      $"&pagina={page}&itens=100";

            CamaraEventPage? eventsPage;
            try
            {
                using var resp = await client.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[AttendanceSync] Events page {Page}: HTTP {Status}", page, (int)resp.StatusCode);
                    break;
                }
                var json = await resp.Content.ReadAsStringAsync(ct);
                eventsPage = JsonSerializer.Deserialize<CamaraEventPage>(json, JsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AttendanceSync] Failed to fetch events page {Page}", page);
                break;
            }

            if (eventsPage?.Dados == null || eventsPage.Dados.Count == 0) break;

            // Filter: only plenary sessions (orgao = Plenário OR descricaoTipo contains "Sessão")
            foreach (var ev in eventsPage.Dados)
            {
                var tipo = ev.DescricaoTipo ?? "";
                var isPlenaryByType = PlenarySessionTypes.Any(t =>
                    tipo.Contains(t, StringComparison.OrdinalIgnoreCase));
                var isPlenaryByOrg = ev.Orgaos?.Any(o =>
                    (o.Nome ?? "").Contains("Plenário", StringComparison.OrdinalIgnoreCase) ||
                    (o.Sigla ?? "").Equals("PLEN", StringComparison.OrdinalIgnoreCase)) ?? false;

                if (isPlenaryByType || isPlenaryByOrg)
                    results.Add(ev);
            }

            // Pagination via links
            var nextLink = eventsPage.Links?
                .FirstOrDefault(l => string.Equals(l.Rel, "next", StringComparison.OrdinalIgnoreCase))
                ?.Href;

            if (string.IsNullOrWhiteSpace(nextLink)) break;
            page++;
            await Task.Delay(150, ct);
        }

        return results;
    }

    // ── Step 2: fetch attendance for one event ────────────────────────────────

    private async Task<int> SyncEventAttendanceAsync(
        HttpClient client,
        CamaraEventSummary session,
        Dictionary<string, int> deputyLookup,
        CancellationToken ct)
    {
        var url = $"{CamaraBaseUrl}/eventos/{session.Id}/deputados";

        CamaraEventDeputiesPage? page;
        try
        {
            using var resp = await client.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Not all events have a deputy list; this is normal
                _logger.LogDebug("[AttendanceSync] Event {Id} has no deputy attendance data", session.Id);
                return 0;
            }
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("[AttendanceSync] Event {Id}: HTTP {Status}", session.Id, (int)resp.StatusCode);
                return 0;
            }
            var json = await resp.Content.ReadAsStringAsync(ct);
            page = JsonSerializer.Deserialize<CamaraEventDeputiesPage>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[AttendanceSync] Event {Id}: request failed", session.Id);
            return 0;
        }

        if (page?.Dados == null || page.Dados.Count == 0) return 0;

        // Parse session date from dataHoraInicio
        if (!DateTime.TryParse(session.DataHoraInicio, out var sessionDateRaw)) return 0;
        var sessionDate = DateTime.SpecifyKind(sessionDateRaw.Date, DateTimeKind.Utc);

        var toAdd = new List<SessionAttendance>();
        var eventIdStr = session.Id.ToString();

        foreach (var deputy in page.Dados)
        {
            var externalId = deputy.Id.ToString();
            if (!deputyLookup.TryGetValue(externalId, out var politicianId)) continue;

            var extKey = $"{eventIdStr}-{externalId}";
            if (extKey.Length > 50) extKey = extKey[..50];

            var situacao = deputy.Situacao ?? "";
            var isPresent = PresentLabels.Contains(situacao) ||
                            situacao.StartsWith("Present", StringComparison.OrdinalIgnoreCase);
            var absenceReason = isPresent ? null
                : (situacao.Length > 100 ? situacao[..100] : (string.IsNullOrWhiteSpace(situacao) ? null : situacao));

            toAdd.Add(new SessionAttendance
            {
                PoliticianId = politicianId,
                SessionDate = sessionDate,
                IsPresent = isPresent,
                AbsenceReason = absenceReason,
                AbsenceJustification = session.DescricaoTipo?.Length > 500
                    ? session.DescricaoTipo[..500]
                    : session.DescricaoTipo,
                Chamber = "Câmara",
                ExternalId = extKey,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (toAdd.Count == 0) return 0;

        // Persist — skip duplicates already in DB
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var existingKeys = (await db.SessionAttendances
            .Where(a => a.ExternalId != null
                     && EF.Functions.Like(a.ExternalId, $"{eventIdStr}-%"))
            .Select(a => a.ExternalId!)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fresh = toAdd.Where(a => !existingKeys.Contains(a.ExternalId!)).ToList();
        if (fresh.Count == 0) return 0;

        db.SessionAttendances.AddRange(fresh);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("[AttendanceSync] Event {Id} ({Date:yyyy-MM-dd} — {Tipo}): +{Count} attendance records",
            session.Id, sessionDate, session.DescricaoTipo, fresh.Count);

        return fresh.Count;
    }

    // ── Deserialization models ────────────────────────────────────────────────

    private sealed class CamaraEventPage
    {
        public List<CamaraEventSummary>? Dados { get; set; }
        public List<CamaraLink>? Links { get; set; }
    }

    private sealed class CamaraEventSummary
    {
        public long Id { get; set; }
        public string? DataHoraInicio { get; set; }
        public string? DataHoraFim { get; set; }
        public string? Situacao { get; set; }
        public string? DescricaoTipo { get; set; }
        public string? Descricao { get; set; }
        public List<CamaraEventOrgao>? Orgaos { get; set; }
    }

    private sealed class CamaraEventOrgao
    {
        public string? Sigla { get; set; }
        public string? Nome { get; set; }
    }

    private sealed class CamaraEventDeputiesPage
    {
        public List<CamaraEventDeputy>? Dados { get; set; }
    }

    private sealed class CamaraEventDeputy
    {
        public int Id { get; set; }
        public string? Nome { get; set; }
        public string? SiglaPartido { get; set; }
        public string? Situacao { get; set; }
    }

    private sealed class CamaraLink
    {
        public string? Rel { get; set; }
        public string? Href { get; set; }
    }
}

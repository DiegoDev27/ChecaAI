using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Api.Hubs;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly ChecaAIDbContext _db;
    private readonly IHubContext<PlenaryHub> _hub;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        ChecaAIDbContext db,
        IHubContext<PlenaryHub> hub,
        ILogger<AlertsController> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    /// <summary>
    /// Returns the most recent voting alerts. Also broadcasts any pending
    /// SignalR notifications that the Worker could not send directly.
    /// </summary>
    /// <param name="limit">Maximum number of alerts to return (default 20)</param>
    /// <param name="chamber">Filter by chamber: "Câmara" or "Senado"</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AlertResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int limit = 20,
        [FromQuery] string? chamber = null)
    {
        var query = _db.VotingAlerts
            .Include(a => a.VotingSession)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(chamber))
            query = query.Where(a => a.VotingSession.Chamber == chamber);

        var alerts = await query
            .OrderByDescending(a => a.DetectedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync();

        // Broadcast any alerts that were not yet sent via SignalR
        // (Worker writes to DB; API hub fans out to connected web clients)
        var unsent = alerts.Where(a => !a.SignalRSent).ToList();
        foreach (var alert in unsent)
        {
            var payload = new VotingAlertPayload
            {
                AlertId = alert.Id,
                SessionId = alert.VotingSessionId,
                ExternalId = alert.VotingSession.ExternalId,
                Chamber = alert.VotingSession.Chamber,
                AlertLevel = alert.AlertLevel,
                Score = alert.Score,
                SummaryText = alert.SummaryText ?? string.Empty,
                DetectedAt = alert.DetectedAt,
                Description = alert.VotingSession.Description
            };

            await PlenaryHub.BroadcastAlertAsync(_hub, payload);
            alert.SignalRSent = true;
            _logger.LogInformation("[AlertsController] SignalR broadcast for alert {Id} ({Level})",
                alert.Id, alert.AlertLevel);
        }

        if (unsent.Any())
            await _db.SaveChangesAsync();

        var dtos = alerts.Select(a => new AlertResponseDto
        {
            Id = a.Id,
            VotingSessionId = a.VotingSessionId,
            ExternalId = a.VotingSession.ExternalId,
            Chamber = a.VotingSession.Chamber,
            Description = a.VotingSession.Description,
            VotingDate = a.VotingSession.VotingDate,
            AlertLevel = a.AlertLevel,
            Score = a.Score,
            ScoreBreakdown = a.ScoreBreakdown,
            SummaryText = a.SummaryText,
            DetectedAt = a.DetectedAt,
            SignalRSent = a.SignalRSent,
            PushSent = a.PushSent
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Returns a single alert by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AlertResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAlert(int id)
    {
        var alert = await _db.VotingAlerts
            .Include(a => a.VotingSession)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alert == null) return NotFound();

        return Ok(new AlertResponseDto
        {
            Id = alert.Id,
            VotingSessionId = alert.VotingSessionId,
            ExternalId = alert.VotingSession.ExternalId,
            Chamber = alert.VotingSession.Chamber,
            Description = alert.VotingSession.Description,
            VotingDate = alert.VotingSession.VotingDate,
            AlertLevel = alert.AlertLevel,
            Score = alert.Score,
            ScoreBreakdown = alert.ScoreBreakdown,
            SummaryText = alert.SummaryText,
            DetectedAt = alert.DetectedAt,
            SignalRSent = alert.SignalRSent,
            PushSent = alert.PushSent
        });
    }
}

// ── Response DTO ──────────────────────────────────────────────────────────────

public class AlertResponseDto
{
    public int Id { get; set; }
    public int VotingSessionId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Chamber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime VotingDate { get; set; }
    public string AlertLevel { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? ScoreBreakdown { get; set; }
    public string? SummaryText { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool SignalRSent { get; set; }
    public bool PushSent { get; set; }
}

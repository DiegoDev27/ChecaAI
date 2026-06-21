using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Api.Dtos;

namespace ChecaAI.Api.Controllers;

/// <summary>
/// Endpoints for voting sessions (votações nominais e simbólicas) from Câmara and Senado.
/// </summary>
[ApiController]
[Route("api/sessions")]
public class VotingSessionsController : ControllerBase
{
    private readonly ChecaAIDbContext _db;

    public VotingSessionsController(ChecaAIDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a paginated list of voting sessions, newest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<VotingSessionListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<VotingSessionListDto>>> GetSessions(
        [FromQuery] string? chamber = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? result = null,
        [FromQuery] bool? hasAlert = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.VotingSessions
            .Include(vs => vs.Proposal)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(chamber))
            query = query.Where(vs => vs.Chamber == chamber);

        if (from.HasValue)
            query = query.Where(vs => vs.VotingDate >= from.Value);

        if (to.HasValue)
            query = query.Where(vs => vs.VotingDate <= to.Value);

        if (!string.IsNullOrWhiteSpace(result))
            query = query.Where(vs => vs.Result == result);

        if (hasAlert.HasValue)
            query = hasAlert.Value
                ? query.Where(vs => vs.Alerts.Any())
                : query.Where(vs => !vs.Alerts.Any());

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(vs => vs.VotingDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(vs => new VotingSessionListDto
            {
                Id = vs.Id,
                ExternalId = vs.ExternalId,
                Description = vs.Description,
                VotingDate = vs.VotingDate,
                SessionType = vs.SessionType,
                TotalVotes = vs.TotalVotes,
                VotesYes = vs.VotesYes,
                VotesNo = vs.VotesNo,
                VotesAbstention = vs.VotesAbstention,
                VotesAbsent = vs.VotesAbsent,
                Result = vs.Result,
                Chamber = vs.Chamber,
                ProposalId = vs.ProposalId,
                ProposalTitle = vs.Proposal != null ? vs.Proposal.Title : null,
                ProposalType = vs.Proposal != null ? vs.Proposal.Type : null,
                HasAlert = vs.Alerts.Any()
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<VotingSessionListDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    /// <summary>Returns a voting session with vote breakdown by politician.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(int id, CancellationToken ct)
    {
        var session = await _db.VotingSessions
            .Include(vs => vs.Proposal)
            .AsNoTracking()
            .FirstOrDefaultAsync(vs => vs.Id == id, ct);

        if (session == null) return NotFound(new { message = "Sessão de votação não encontrada" });

        // Individual votes (paginated to first 50 for perf)
        var votes = await _db.Votes
            .Where(v => v.VotingSessionId == id)
            .Include(v => v.Politician)
            .OrderBy(v => v.Politician.FullName)
            .AsNoTracking()
            .Select(v => new
            {
                v.Id,
                v.VoteValue,
                Politician = new { v.Politician.Id, v.Politician.FullName, v.Politician.Party, v.Politician.State }
            })
            .Take(600) // Nominal session in Câmara has at most 513 votes
            .ToListAsync(ct);

        return Ok(new
        {
            session.Id,
            session.ExternalId,
            session.Description,
            session.VotingDate,
            session.SessionType,
            session.TotalVotes,
            session.VotesYes,
            session.VotesNo,
            session.VotesAbstention,
            session.VotesAbsent,
            session.Result,
            session.Chamber,
            Proposal = session.Proposal == null ? null : new
            {
                session.Proposal.Id,
                session.Proposal.Title,
                session.Proposal.Type,
                session.Proposal.Number,
                session.Proposal.Year,
                session.Proposal.Status
            },
            Votes = votes,
            VotesSummary = new
            {
                Yes = votes.Count(v => v.VoteValue == "Yes"),
                No = votes.Count(v => v.VoteValue == "No"),
                Abstention = votes.Count(v => v.VoteValue == "Abstention"),
                Absent = votes.Count(v => v.VoteValue == "Absent")
            }
        });
    }
}

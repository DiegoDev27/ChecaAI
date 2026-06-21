using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Api.Dtos;

namespace ChecaAI.Api.Controllers;

/// <summary>
/// Endpoints for legislative proposals (PL, PEC, MP, etc.) from Câmara and Senado.
/// </summary>
[ApiController]
[Route("api/proposals")]
public class ProposalsController : ControllerBase
{
    private readonly ChecaAIDbContext _db;

    public ProposalsController(ChecaAIDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a paginated list of proposals with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProposalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProposalDto>>> GetProposals(
        [FromQuery] string? q = null,
        [FromQuery] string? type = null,
        [FromQuery] int? year = null,
        [FromQuery] string? chamber = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Proposals.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Title.ToLower().Contains(q.ToLower()) ||
                                     p.Summary != null && p.Summary.ToLower().Contains(q.ToLower()));

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.Type == type);

        if (year.HasValue)
            query = query.Where(p => p.Year == year.Value);

        if (!string.IsNullOrWhiteSpace(chamber))
            query = query.Where(p => p.Chamber == chamber);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.ProposalDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProposalDto
            {
                Id = p.Id,
                ExternalId = p.ExternalId,
                Title = p.Title,
                Summary = p.Summary,
                Type = p.Type,
                Number = p.Number,
                Year = p.Year,
                Chamber = p.Chamber,
                Author = p.Author,
                Status = p.Status,
                ProposalDate = p.ProposalDate,
                VotingSessionCount = p.VotingSessions.Count
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<ProposalDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    /// <summary>
    /// Returns a proposal with its voting sessions.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProposalDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProposalDetailDto>> GetProposal(int id, CancellationToken ct)
    {
        var p = await _db.Proposals
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p == null) return NotFound(new { message = "Proposição não encontrada" });

        var sessions = await _db.VotingSessions
            .Where(vs => vs.ProposalId == id)
            .OrderByDescending(vs => vs.VotingDate)
            .AsNoTracking()
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
                HasAlert = vs.Alerts.Any()
            })
            .ToListAsync(ct);

        return Ok(new ProposalDetailDto
        {
            Id = p.Id,
            ExternalId = p.ExternalId,
            Title = p.Title,
            Summary = p.Summary,
            Type = p.Type,
            Number = p.Number,
            Year = p.Year,
            Chamber = p.Chamber,
            Author = p.Author,
            Status = p.Status,
            ProposalDate = p.ProposalDate,
            VotingSessionCount = sessions.Count,
            VotingSessions = sessions
        });
    }
}

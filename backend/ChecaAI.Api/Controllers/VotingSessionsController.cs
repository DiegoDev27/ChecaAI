using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotingSessionsController : ControllerBase
{
    private readonly ChecaAIDbContext _context;

    public VotingSessionsController(ChecaAIDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VotingSession>>> GetVotingSessions(
        [FromQuery] string? chamber = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _context.VotingSessions
            .Include(vs => vs.Proposal)
            .AsQueryable();

        if (!string.IsNullOrEmpty(chamber))
            query = query.Where(vs => vs.Chamber == chamber);

        if (fromDate.HasValue)
            query = query.Where(vs => vs.VotingDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(vs => vs.VotingDate <= toDate.Value);

        return await query
            .OrderByDescending(vs => vs.VotingDate)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VotingSession>> GetVotingSession(int id)
    {
        var votingSession = await _context.VotingSessions
            .Include(vs => vs.Proposal)
            .Include(vs => vs.Votes)
                .ThenInclude(v => v.Politician)
            .FirstOrDefaultAsync(vs => vs.Id == id);

        if (votingSession == null)
        {
            return NotFound();
        }

        return votingSession;
    }

    [HttpGet("{id}/votes")]
    public async Task<ActionResult<IEnumerable<Vote>>> GetVotingSessionVotes(int id)
    {
        var votingSession = await _context.VotingSessions.FindAsync(id);
        if (votingSession == null)
        {
            return NotFound();
        }

        var votes = await _context.Votes
            .Include(v => v.Politician)
            .Where(v => v.VotingSessionId == id)
            .OrderBy(v => v.Politician.FullName)
            .ToListAsync();

        return votes;
    }

    [HttpGet("{id}/results")]
    public async Task<ActionResult<object>> GetVotingSessionResults(int id)
    {
        var votingSession = await _context.VotingSessions
            .Include(vs => vs.Proposal)
            .Include(vs => vs.Votes)
                .ThenInclude(v => v.Politician)
            .FirstOrDefaultAsync(vs => vs.Id == id);

        if (votingSession == null)
        {
            return NotFound();
        }

        var votesByValue = votingSession.Votes
            .GroupBy(v => v.VoteValue)
            .ToDictionary(g => g.Key, g => g.Select(v => new
            {
                PoliticianId = v.Politician.Id,
                PoliticianName = v.Politician.FullName,
                Party = v.Politician.Party,
                State = v.Politician.State
            }).ToList());

        var result = new
        {
            VotingSession = new
            {
                votingSession.Id,
                votingSession.Description,
                votingSession.VotingDate,
                votingSession.Result,
                votingSession.TotalVotes,
                votingSession.VotesYes,
                votingSession.VotesNo,
                votingSession.VotesAbstention,
                votingSession.VotesAbsent
            },
            Proposal = new
            {
                votingSession.Proposal.Id,
                votingSession.Proposal.Title,
                votingSession.Proposal.Type,
                votingSession.Proposal.Number,
                votingSession.Proposal.Year
            },
            VotesByValue = votesByValue
        };

        return result;
    }
}
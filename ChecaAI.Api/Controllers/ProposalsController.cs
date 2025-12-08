using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProposalsController : ControllerBase
{
    private readonly ChecaAIDbContext _context;

    public ProposalsController(ChecaAIDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Proposal>>> GetProposals(
        [FromQuery] string? type = null,
        [FromQuery] int? year = null,
        [FromQuery] string? chamber = null,
        [FromQuery] string? status = null)
    {
        var query = _context.Proposals.AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(p => p.Type == type);

        if (year.HasValue)
            query = query.Where(p => p.Year == year);

        if (!string.IsNullOrEmpty(chamber))
            query = query.Where(p => p.Chamber == chamber);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);

        return await query
            .OrderByDescending(p => p.ProposalDate)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Proposal>> GetProposal(int id)
    {
        var proposal = await _context.Proposals
            .Include(p => p.VotingSessions)
                .ThenInclude(vs => vs.Votes)
                    .ThenInclude(v => v.Politician)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null)
        {
            return NotFound();
        }

        return proposal;
    }

    [HttpGet("{id}/voting-sessions")]
    public async Task<ActionResult<IEnumerable<VotingSession>>> GetProposalVotingSessions(int id)
    {
        var proposal = await _context.Proposals.FindAsync(id);
        if (proposal == null)
        {
            return NotFound();
        }

        var votingSessions = await _context.VotingSessions
            .Include(vs => vs.Votes)
                .ThenInclude(v => v.Politician)
            .Where(vs => vs.ProposalId == id)
            .OrderByDescending(vs => vs.VotingDate)
            .ToListAsync();

        return votingSessions;
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliticiansController : ControllerBase
{
    private readonly ChecaAIDbContext _context;

    public PoliticiansController(ChecaAIDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Politician>>> GetPoliticians(
        [FromQuery] string? state = null,
        [FromQuery] string? position = null,
        [FromQuery] string? party = null)
    {
        var query = _context.Politicians.Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(state))
            query = query.Where(p => p.State == state);

        if (!string.IsNullOrEmpty(position))
            query = query.Where(p => p.PoliticalPosition.Contains(position));

        if (!string.IsNullOrEmpty(party))
            query = query.Where(p => p.Party == party);

        return await query.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Politician>> GetPolitician(int id)
    {
        var politician = await _context.Politicians
            .Include(p => p.Votes)
                .ThenInclude(v => v.VotingSession)
                    .ThenInclude(vs => vs.Proposal)
            .Include(p => p.Expenses)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (politician == null)
        {
            return NotFound();
        }

        return politician;
    }

    [HttpGet("{id}/votes")]
    public async Task<ActionResult<IEnumerable<Vote>>> GetPoliticianVotes(int id)
    {
        var politician = await _context.Politicians.FindAsync(id);
        if (politician == null)
        {
            return NotFound();
        }

        var votes = await _context.Votes
            .Include(v => v.VotingSession)
                .ThenInclude(vs => vs.Proposal)
            .Where(v => v.PoliticianId == id)
            .OrderByDescending(v => v.VotingSession.VotingDate)
            .ToListAsync();

        return votes;
    }

    [HttpGet("{id}/expenses")]
    public async Task<ActionResult<IEnumerable<PoliticianExpense>>> GetPoliticianExpenses(
        int id,
        [FromQuery] int? year = null,
        [FromQuery] string? category = null)
    {
        var politician = await _context.Politicians.FindAsync(id);
        if (politician == null)
        {
            return NotFound();
        }

        var query = _context.PoliticianExpenses.Where(e => e.PoliticianId == id);

        if (year.HasValue)
            query = query.Where(e => e.Year == year);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(e => e.Category.Contains(category));

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();

        return expenses;
    }
}
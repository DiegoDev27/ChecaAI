using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Domain.Entities;
using ChecaAI.Application.Interfaces;

namespace ChecaAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliticiansController : ControllerBase
{
    private readonly ChecaAIDbContext _context;
    private readonly ISenateService _senateService;

    public PoliticiansController(ChecaAIDbContext context, ISenateService senateService)
    {
        _context = context;
        _senateService = senateService;
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

    // Senate-specific endpoints
    [HttpGet("senators")]
    public async Task<ActionResult<IEnumerable<Politician>>> GetSenators(
        [FromQuery] string? state = null,
        [FromQuery] string? party = null,
        [FromQuery] string? bloc = null)
    {
        var query = _context.Politicians
            .Include(p => p.PoliticalBloc)
            .Include(p => p.Phones)
            .Where(p => p.PoliticalPosition == "Senator" && p.IsActive);

        if (!string.IsNullOrEmpty(state))
            query = query.Where(p => p.State == state);

        if (!string.IsNullOrEmpty(party))
            query = query.Where(p => p.Party == party);

        if (!string.IsNullOrEmpty(bloc))
            query = query.Where(p => p.PoliticalBloc != null && p.PoliticalBloc.Name.Contains(bloc));

        return await query.ToListAsync();
    }

    [HttpGet("senators/{externalId}")]
    public async Task<ActionResult<Politician>> GetSenatorByExternalId(string externalId)
    {
        var senator = await _senateService.GetSenatorByIdAsync(externalId);
        
        if (senator == null)
        {
            return NotFound();
        }

        return senator;
    }

    [HttpPost("senators/sync")]
    public async Task<ActionResult> SyncSenatorsFromApi()
    {
        try
        {
            var senators = await _senateService.GetSenatorsAsync();
            return Ok(new { 
                message = "Senators synchronized successfully", 
                count = senators.Count() 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                message = "Error synchronizing senators", 
                error = ex.Message 
            });
        }
    }

    [HttpGet("senators/{id:int}/mandates")]
    public async Task<ActionResult<IEnumerable<PoliticianMandate>>> GetSenatorMandates(int id)
    {
        var senator = await _context.Politicians
            .Include(p => p.Mandates)
                .ThenInclude(m => m.Legislatures)
            .Include(p => p.Mandates)
                .ThenInclude(m => m.Substitutes)
                    .ThenInclude(s => s.SubstitutePolitician)
            .Include(p => p.Mandates)
                .ThenInclude(m => m.Exercises)
            .FirstOrDefaultAsync(p => p.Id == id && p.PoliticalPosition == "Senator");

        if (senator == null)
        {
            return NotFound();
        }

        return senator.Mandates.ToList();
    }

    [HttpGet("political-blocs")]
    public async Task<ActionResult<IEnumerable<PoliticalBloc>>> GetPoliticalBlocs()
    {
        var blocs = await _context.PoliticalBlocs
            .Include(b => b.Politicians.Where(p => p.IsActive))
            .OrderBy(b => b.Name)
            .ToListAsync();

        return blocs;
    }
}
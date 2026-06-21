using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Api.Dtos;

namespace ChecaAI.Api.Controllers;

/// <summary>
/// Endpoints for Brazilian political parties.
/// </summary>
[ApiController]
[Route("api/parties")]
public class PartiesController : ControllerBase
{
    private readonly ChecaAIDbContext _db;

    public PartiesController(ChecaAIDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns all active political parties with member counts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PartyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PartyDto>>> GetParties(
        [FromQuery] bool? active = true,
        CancellationToken ct = default)
    {
        var query = _db.Parties.AsNoTracking();

        if (active.HasValue)
            query = query.Where(p => p.IsActive == active.Value);

        var parties = await query
            .OrderBy(p => p.Acronym)
            .Select(p => new PartyDto
            {
                Id = p.Id,
                Acronym = p.Acronym,
                FullName = p.FullName,
                Number = p.Number,
                President = p.President,
                IsActive = p.IsActive,
                MemberCount = _db.Politicians.Count(pol => pol.Party == p.Acronym && pol.IsActive)
            })
            .ToListAsync(ct);

        return Ok(parties);
    }

    /// <summary>Returns full party detail by acronym.</summary>
    [HttpGet("{acronym}")]
    [ProducesResponseType(typeof(PartyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDto>> GetParty(string acronym, CancellationToken ct)
    {
        var party = await _db.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Acronym == acronym.ToUpperInvariant(), ct);

        if (party == null) return NotFound(new { message = "Partido não encontrado" });

        var memberCount = await _db.Politicians
            .CountAsync(p => p.Party == party.Acronym && p.IsActive, ct);

        return Ok(new PartyDto
        {
            Id = party.Id,
            Acronym = party.Acronym,
            FullName = party.FullName,
            Number = party.Number,
            President = party.President,
            IsActive = party.IsActive,
            MemberCount = memberCount
        });
    }

    /// <summary>Returns members of a specific party.</summary>
    [HttpGet("{acronym}/members")]
    [ProducesResponseType(typeof(PagedResponse<PoliticianListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PoliticianListDto>>> GetMembers(
        string acronym,
        [FromQuery] string? position = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Politicians
            .Where(p => p.Party == acronym && p.IsActive)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(position))
            query = query.Where(p => p.PoliticalPosition == position);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PoliticianListDto
            {
                Id = p.Id,
                FullName = p.FullName,
                PoliticalPosition = p.PoliticalPosition,
                Party = p.Party,
                State = p.State,
                City = p.City,
                PhotoUrl = p.PhotoUrl,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<PoliticianListDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Api.Dtos;

namespace ChecaAI.Api.Controllers;

/// <summary>
/// Endpoints for parliamentary committees (Câmara and Senado).
/// </summary>
[ApiController]
[Route("api/committees")]
public class CommitteesController : ControllerBase
{
    private readonly ChecaAIDbContext _db;

    public CommitteesController(ChecaAIDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns a paginated list of committees with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CommitteeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CommitteeDto>>> GetCommittees(
        [FromQuery] string? chamber = null,
        [FromQuery] string? type = null,
        [FromQuery] bool? active = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Committees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(chamber))
            query = query.Where(c => c.Chamber == chamber);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(c => c.CommitteeType == type);

        if (active.HasValue)
            query = query.Where(c => c.IsActive == active.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Chamber)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommitteeDto
            {
                Id = c.Id,
                Name = c.Name,
                Acronym = c.Acronym,
                CommitteeType = c.CommitteeType,
                Chamber = c.Chamber,
                IsActive = c.IsActive,
                MemberCount = c.Members.Count
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<CommitteeDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    /// <summary>Returns a committee by ID with its members.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCommittee(int id, CancellationToken ct)
    {
        var committee = await _db.Committees
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (committee == null) return NotFound(new { message = "Comissão não encontrada" });

        var members = await _db.CommitteeMemberships
            .Where(m => m.CommitteeId == id)
            .AsNoTracking()
            .OrderBy(m => m.Role)
            .ThenBy(m => m.Politician.FullName)
            .Select(m => new
            {
                m.Id,
                m.Role,
                Politician = new
                {
                    m.Politician.Id,
                    m.Politician.FullName,
                    m.Politician.Party,
                    m.Politician.State,
                    m.Politician.PhotoUrl
                }
            })
            .ToListAsync(ct);

        return Ok(new
        {
            committee.Id,
            committee.Name,
            committee.Acronym,
            committee.CommitteeType,
            committee.Chamber,
            committee.IsActive,
            MemberCount = members.Count,
            Members = members
        });
    }

    /// <summary>Returns all committee memberships for a politician.</summary>
    [HttpGet("politicians/{politicianId:int}")]
    [ProducesResponseType(typeof(List<CommitteeMembershipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CommitteeMembershipDto>>> GetPoliticianCommittees(
        int politicianId,
        CancellationToken ct)
    {
        if (!await _db.Politicians.AnyAsync(p => p.Id == politicianId, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var memberships = await _db.CommitteeMemberships
            .Where(m => m.PoliticianId == politicianId)
            .Include(m => m.Committee)
            .Where(m => m.Committee.IsActive)
            .AsNoTracking()
            .OrderBy(m => m.Committee.Chamber)
            .ThenBy(m => m.Committee.Name)
            .Select(m => new CommitteeMembershipDto
            {
                CommitteeId = m.CommitteeId,
                CommitteeName = m.Committee.Name,
                Acronym = m.Committee.Acronym,
                CommitteeType = m.Committee.CommitteeType,
                Chamber = m.Committee.Chamber,
                Role = m.Role
            })
            .ToListAsync(ct);

        return Ok(memberships);
    }
}

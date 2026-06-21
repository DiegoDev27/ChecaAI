using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Api.Dtos;

namespace ChecaAI.Api.Controllers;

/// <summary>
/// Endpoints for Brazilian politicians at all levels:
/// senators, federal deputies, state deputies, mayors, city councilors, governors, president.
/// </summary>
[ApiController]
[Route("api/politicians")]
public class PoliticiansController : ControllerBase
{
    private readonly ChecaAIDbContext _db;

    public PoliticiansController(ChecaAIDbContext db)
    {
        _db = db;
    }

    // ── List / Search ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated list of politicians with optional filters.
    /// </summary>
    /// <param name="q">Search by name (partial match, case-insensitive)</param>
    /// <param name="position">Federal Deputy | Senator | Governor | Mayor | State Deputy | City Councilor</param>
    /// <param name="state">2-letter UF code (SP, RJ, MG...)</param>
    /// <param name="party">Party acronym (PT, PL, MDB...)</param>
    /// <param name="page">Page number, 1-based (default 1)</param>
    /// <param name="pageSize">Results per page, max 100 (default 20)</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PoliticianListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PoliticianListDto>>> GetPoliticians(
        [FromQuery] string? q = null,
        [FromQuery] string? position = null,
        [FromQuery] string? state = null,
        [FromQuery] string? party = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Politicians
            .Where(p => p.IsActive)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.FullName.ToLower().Contains(q.ToLower()));

        if (!string.IsNullOrWhiteSpace(position))
            query = query.Where(p => p.PoliticalPosition == position);

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(p => p.State == state);

        if (!string.IsNullOrWhiteSpace(party))
            query = query.Where(p => p.Party == party);

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
            Data = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    // ── Detail ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns full politician profile with recent votes, expense summary, committees, and salary.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PoliticianDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PoliticianDetailDto>> GetPolitician(int id, CancellationToken ct)
    {
        var p = await _db.Politicians
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p == null) return NotFound(new { message = "Parlamentar não encontrado" });

        // Recent votes (last 20)
        var recentVotes = await _db.Votes
            .Where(v => v.PoliticianId == id)
            .Include(v => v.VotingSession)
            .OrderByDescending(v => v.VotingSession.VotingDate)
            .Take(20)
            .AsNoTracking()
            .Select(v => new RecentVoteDto
            {
                SessionId = v.VotingSessionId,
                VoteValue = v.VoteValue,
                VotingDate = v.VotingSession.VotingDate,
                Description = v.VotingSession.Description,
                Result = v.VotingSession.Result,
                Chamber = v.VotingSession.Chamber
            })
            .ToListAsync(ct);

        // Vote stats (all-time)
        var voteStats = await _db.Votes
            .Where(v => v.PoliticianId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Yes = g.Count(v => v.VoteValue == "Yes"),
                No = g.Count(v => v.VoteValue == "No"),
                Abstention = g.Count(v => v.VoteValue == "Abstention"),
                Absent = g.Count(v => v.VoteValue == "Absent")
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        // Expense summary — prefer current year, fall back to most recent year with data
        var currentYear = DateTime.UtcNow.Year;
        var bestExpenseYear = await _db.PoliticianExpenses
            .Where(e => e.PoliticianId == id)
            .GroupBy(e => e.Year)
            .OrderByDescending(g => g.Key == currentYear ? 1 : 0) // prefer current year
            .ThenByDescending(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        ExpenseSummaryDto? expenseSummary = null;
        if (bestExpenseYear > 0)
        {
            var categoryStats = await _db.PoliticianExpenses
                .Where(e => e.PoliticianId == id && e.Year == bestExpenseYear)
                .GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
                .OrderByDescending(x => x.Total)
                .Take(10)
                .AsNoTracking()
                .ToListAsync(ct);

            if (categoryStats.Count > 0)
            {
                expenseSummary = new ExpenseSummaryDto
                {
                    Year = bestExpenseYear,
                    Total = categoryStats.Sum(c => c.Total),
                    Count = categoryStats.Sum(c => c.Count),
                    ByCategory = categoryStats.Select(c => new ExpenseCategoryDto
                    {
                        Category = c.Category,
                        Total = c.Total,
                        Count = c.Count
                    }).ToList()
                };
            }
        }

        // Committees (active)
        var committees = await _db.CommitteeMemberships
            .Where(m => m.PoliticianId == id)
            .Include(m => m.Committee)
            .Where(m => m.Committee.IsActive)
            .AsNoTracking()
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

        // Latest salary
        var salary = await _db.PoliticianSalaries
            .Where(s => s.PoliticianId == id)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return Ok(new PoliticianDetailDto
        {
            Id = p.Id,
            FullName = p.FullName,
            PoliticalPosition = p.PoliticalPosition,
            Party = p.Party,
            State = p.State,
            City = p.City,
            PhotoUrl = p.PhotoUrl,
            Email = p.Email,
            Website = p.PersonalPageUrl,
            IsActive = p.IsActive,
            ExternalId = p.ExternalId,
            VoteStats = voteStats == null ? null : new VoteStatsDto
            {
                Total = voteStats.Total,
                Yes = voteStats.Yes,
                No = voteStats.No,
                Abstention = voteStats.Abstention,
                Absent = voteStats.Absent
            },
            RecentVotes = recentVotes,
            ExpenseSummary = expenseSummary,
            Committees = committees,
            LatestSalary = salary == null ? null : new SalaryDto
            {
                Id = salary.Id,
                GrossSalary = salary.GrossSalary,
                NetSalary = salary.NetSalary,
                Allowances = salary.Allowances,
                Month = salary.Month,
                Year = salary.Year,
                Source = salary.Source
            }
        });
    }

    // ── Votes ──────────────────────────────────────────────────────────────────

    /// <summary>Returns paginated vote history for a politician.</summary>
    [HttpGet("{id:int}/votes")]
    [ProducesResponseType(typeof(PagedResponse<RecentVoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<RecentVoteDto>>> GetVotes(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!await _db.Politicians.AnyAsync(p => p.Id == id, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var query = _db.Votes
            .Where(v => v.PoliticianId == id)
            .Include(v => v.VotingSession)
            .AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(v => v.VotingSession.VotingDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new RecentVoteDto
            {
                SessionId = v.VotingSessionId,
                VoteValue = v.VoteValue,
                VotingDate = v.VotingSession.VotingDate,
                Description = v.VotingSession.Description,
                Result = v.VotingSession.Result,
                Chamber = v.VotingSession.Chamber
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<RecentVoteDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    // ── Expenses ───────────────────────────────────────────────────────────────

    /// <summary>Returns paginated parliamentary quota expenses for a politician.</summary>
    [HttpGet("{id:int}/expenses")]
    [ProducesResponseType(typeof(PagedResponse<ExpenseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ExpenseDto>>> GetExpenses(
        int id,
        [FromQuery] int? year = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!await _db.Politicians.AnyAsync(p => p.Id == id, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var query = _db.PoliticianExpenses
            .Where(e => e.PoliticianId == id)
            .AsNoTracking();

        if (year.HasValue)
            query = query.Where(e => e.Year == year.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category.ToLower().Contains(category.ToLower()));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExpenseDto
            {
                Id = e.Id,
                Description = e.Description,
                Category = e.Category,
                Amount = e.Amount,
                Provider = e.Provider,
                DocumentNumber = e.DocumentNumber,
                ExpenseDate = e.ExpenseDate,
                Month = e.Month,
                Year = e.Year
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<ExpenseDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    // ── Salaries ───────────────────────────────────────────────────────────────

    /// <summary>Returns salary history for a politician (CGU data).</summary>
    [HttpGet("{id:int}/salaries")]
    [ProducesResponseType(typeof(List<SalaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SalaryDto>>> GetSalaries(int id, CancellationToken ct)
    {
        if (!await _db.Politicians.AnyAsync(p => p.Id == id, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var salaries = await _db.PoliticianSalaries
            .Where(s => s.PoliticianId == id)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .AsNoTracking()
            .Select(s => new SalaryDto
            {
                Id = s.Id,
                GrossSalary = s.GrossSalary,
                NetSalary = s.NetSalary,
                Allowances = s.Allowances,
                Month = s.Month,
                Year = s.Year,
                Source = s.Source
            })
            .ToListAsync(ct);

        return Ok(salaries);
    }
}

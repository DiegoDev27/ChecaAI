using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChecaAI.Infrastructure.Data;
using ChecaAI.Api.Dtos;
using ChecaAI.Application.Interfaces;

namespace ChecaAI.Api.Controllers;

/// <summary>
/// Transparency endpoints: salaries (CGU), campaign expenses, asset declarations,
/// election results and session attendance.
/// Data is served from the local DB (populated by background sync services).
/// If the DB is empty for a politician, an on-demand fetch is attempted for salary.
/// </summary>
[ApiController]
[Route("api/transparency")]
public class TransparencyController : ControllerBase
{
    private readonly ChecaAIDbContext _db;
    private readonly ITseService _tseService;
    private readonly ICguService _cguService;

    public TransparencyController(
        ChecaAIDbContext db,
        ITseService tseService,
        ICguService cguService)
    {
        _db = db;
        _tseService = tseService;
        _cguService = cguService;
    }

    // ── Salaries (CGU) ────────────────────────────────────────────────────────

    /// <summary>Returns salary records for a politician from the CGU portal.</summary>
    [HttpGet("politicians/{id:int}/salary")]
    [ProducesResponseType(typeof(List<SalaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SalaryDto>>> GetSalaries(
        int id,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        var politician = await _db.Politicians.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (politician == null) return NotFound(new { message = "Parlamentar não encontrado" });

        var cached = await _db.PoliticianSalaries
            .Where(s => s.PoliticianId == id)
            .Where(s => !year.HasValue || s.Year == year)
            .Where(s => !month.HasValue || s.Month == month)
            .AsNoTracking()
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
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

        if (cached.Any()) return Ok(cached);

        // On-demand fallback: fetch live from CGU if politician has CPF
        if (string.IsNullOrEmpty(politician.Cpf))
            return Ok(Array.Empty<SalaryDto>());

        var salaries = (await _cguService.GetPoliticianSalariesAsync(politician.Cpf, year, month)).ToList();
        foreach (var salary in salaries)
        {
            salary.PoliticianId = id;
            _db.PoliticianSalaries.Add(salary);
        }
        if (salaries.Any()) await _db.SaveChangesAsync(ct);

        return Ok(salaries.Select(s => new SalaryDto
        {
            Id = s.Id,
            GrossSalary = s.GrossSalary,
            NetSalary = s.NetSalary,
            Allowances = s.Allowances,
            Month = s.Month,
            Year = s.Year,
            Source = s.Source
        }).ToList());
    }

    // ── Campaign Expenses (TSE) ───────────────────────────────────────────────

    /// <summary>Returns campaign expense records for a politician from TSE data.</summary>
    [HttpGet("politicians/{id:int}/campaign-expenses")]
    [ProducesResponseType(typeof(List<CampaignExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CampaignExpenseDto>>> GetCampaignExpenses(
        int id,
        [FromQuery] int? year = null,
        CancellationToken ct = default)
    {
        var politician = await _db.Politicians.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (politician == null) return NotFound(new { message = "Parlamentar não encontrado" });

        var query = _db.CampaignExpenses
            .Where(e => e.PoliticianId == id)
            .AsNoTracking();

        if (year.HasValue)
            query = query.Where(e => e.ElectionYear == year.Value);

        var expenses = await query
            .OrderByDescending(e => e.ElectionYear)
            .Select(e => new CampaignExpenseDto
            {
                Id = e.Id,
                ElectionYear = e.ElectionYear,
                Category = e.Category,
                Description = e.Description,
                Amount = e.Amount,
                Supplier = e.Provider,
                SupplierCnpjCpf = e.ProviderCnpj,
                ExternalId = e.ExternalId
            })
            .ToListAsync(ct);

        return Ok(expenses);
    }

    // ── Asset Declarations (TSE) ──────────────────────────────────────────────

    /// <summary>Returns asset declarations for a politician from TSE data.</summary>
    [HttpGet("politicians/{id:int}/assets")]
    [ProducesResponseType(typeof(List<AssetDeclarationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AssetDeclarationDto>>> GetAssets(
        int id,
        [FromQuery] int? year = null,
        CancellationToken ct = default)
    {
        var politician = await _db.Politicians.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (politician == null) return NotFound(new { message = "Parlamentar não encontrado" });

        var query = _db.AssetDeclarations
            .Where(a => a.PoliticianId == id)
            .AsNoTracking();

        if (year.HasValue)
            query = query.Where(a => a.ElectionYear == year.Value);

        var assets = await query
            .OrderByDescending(a => a.ElectionYear)
            .Select(a => new AssetDeclarationDto
            {
                Id = a.Id,
                ElectionYear = a.ElectionYear,
                AssetType = a.AssetType,
                Description = a.Description,
                DeclaredValue = a.DeclaredValue,
                ExternalId = a.ExternalId
            })
            .ToListAsync(ct);

        return Ok(assets);
    }

    // ── Election Results (TSE) ────────────────────────────────────────────────

    /// <summary>Returns election results for a politician from TSE data.</summary>
    [HttpGet("politicians/{id:int}/election-results")]
    [ProducesResponseType(typeof(List<ElectionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ElectionResultDto>>> GetElectionResults(
        int id,
        CancellationToken ct = default)
    {
        if (!await _db.Politicians.AnyAsync(p => p.Id == id, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var results = await _db.ElectionResults
            .Where(r => r.PoliticianId == id)
            .AsNoTracking()
            .OrderByDescending(r => r.ElectionYear)
            .Select(r => new ElectionResultDto
            {
                Id = r.Id,
                ElectionYear = r.ElectionYear,
                Position = r.Position,
                State = r.State,
                City = r.City,
                VotesReceived = r.VotesReceived,
                TotalVotes = r.TotalVotes,
                VoteShare = r.VoteShare,
                IsElected = r.IsElected,
                ExternalId = r.ExternalId
            })
            .ToListAsync(ct);

        return Ok(results);
    }

    // ── Session Attendance ────────────────────────────────────────────────────

    /// <summary>Returns session attendance records for a politician.</summary>
    [HttpGet("politicians/{id:int}/attendance")]
    [ProducesResponseType(typeof(PagedResponse<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<AttendanceDto>>> GetAttendance(
        int id,
        [FromQuery] int? year = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!await _db.Politicians.AnyAsync(p => p.Id == id, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var query = _db.SessionAttendances
            .Where(a => a.PoliticianId == id)
            .AsNoTracking();

        if (year.HasValue)
            query = query.Where(a => a.SessionDate.Year == year.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.SessionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttendanceDto
            {
                Id = a.Id,
                SessionDate = a.SessionDate,
                IsPresent = a.IsPresent,
                AbsenceReason = a.AbsenceReason,
                AbsenceJustification = a.AbsenceJustification,
                Chamber = a.Chamber
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<AttendanceDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    // ── Cabinet Staff ─────────────────────────────────────────────────────────

    /// <summary>Returns cabinet staff members for a politician (CGU data).</summary>
    [HttpGet("politicians/{id:int}/cabinet-staff")]
    [ProducesResponseType(typeof(PagedResponse<CabinetStaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<CabinetStaffDto>>> GetCabinetStaff(
        int id,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!await _db.Politicians.AnyAsync(p => p.Id == id, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var query = _db.CabinetStaff
            .Where(s => s.PoliticianId == id)
            .AsNoTracking();

        if (year.HasValue)
            query = query.Where(s => s.Year == year.Value);

        if (month.HasValue)
            query = query.Where(s => s.Month == month.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .ThenBy(s => s.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new CabinetStaffDto
            {
                Id = s.Id,
                FullName = s.FullName,
                Role = s.Role,
                GrossSalary = s.GrossSalary,
                NetSalary = s.NetSalary,
                Month = s.Month,
                Year = s.Year,
                StartDate = s.StartDate,
                EndDate = s.EndDate
            })
            .ToListAsync(ct);

        return Ok(new PagedResponse<CabinetStaffDto>
        {
            Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    // ── Allowances ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns parliamentary allowances (auxílio moradia, saúde, alimentação, etc.)
    /// grouped by year/month, ordered newest first.
    /// </summary>
    [HttpGet("politicians/{id:int}/allowances")]
    [ProducesResponseType(typeof(PagedResponse<AllowanceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<AllowanceSummaryDto>>> GetAllowances(
        int id,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 60);

        if (!await _db.Politicians.AnyAsync(p => p.Id == id, ct))
            return NotFound(new { message = "Parlamentar não encontrado" });

        var query = _db.Allowances
            .Where(a => a.PoliticianId == id)
            .AsNoTracking();

        if (year.HasValue)
            query = query.Where(a => a.Year == year.Value);

        if (month.HasValue)
            query = query.Where(a => a.Month == month.Value);

        // Group by year/month for a summary view
        var rawGroups = await query
            .OrderByDescending(a => a.Year).ThenByDescending(a => a.Month)
            .Select(a => new AllowanceDto
            {
                Id = a.Id,
                AllowanceType = a.AllowanceType,
                Amount = a.Amount,
                Month = a.Month,
                Year = a.Year,
                Description = a.Description,
                Source = a.Source
            })
            .ToListAsync(ct);

        var summaries = rawGroups
            .GroupBy(a => (a.Year, a.Month))
            .Select(g => new AllowanceSummaryDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Total = g.Sum(a => a.Amount),
                Items = g.OrderBy(a => a.AllowanceType).ToList()
            })
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .ToList();

        var totalCount = summaries.Count;
        var paged = summaries.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new PagedResponse<AllowanceSummaryDto>
        {
            Data = paged, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }
}

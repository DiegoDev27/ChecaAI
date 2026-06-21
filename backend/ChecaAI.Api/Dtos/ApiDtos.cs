namespace ChecaAI.Api.Dtos;

// ── Generic paged response ────────────────────────────────────────────────────

/// <summary>Standard paginated response envelope used by all list endpoints.</summary>
public sealed class PagedResponse<T>
{
    public List<T> Data { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize));
    public bool HasNextPage => Page < TotalPages;
    public bool HasPrevPage => Page > 1;
}

// ── Politician DTOs ───────────────────────────────────────────────────────────

/// <summary>Lean politician for list views and search results.</summary>
public sealed class PoliticianListDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PoliticalPosition { get; init; } = string.Empty;
    public string? Party { get; init; }
    public string? State { get; init; }
    public string? City { get; init; }
    public string? PhotoUrl { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Rich politician detail with aggregated stats.</summary>
public sealed class PoliticianDetailDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PoliticalPosition { get; init; } = string.Empty;
    public string? Party { get; init; }
    public string? State { get; init; }
    public string? City { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public bool IsActive { get; init; }
    public string? ExternalId { get; init; }

    // Aggregated vote stats (last 100 votes)
    public VoteStatsDto? VoteStats { get; init; }

    // Last 10 votes
    public List<RecentVoteDto> RecentVotes { get; init; } = new();

    // Expense summary (current year)
    public ExpenseSummaryDto? ExpenseSummary { get; init; }

    // Committees (active)
    public List<CommitteeMembershipDto> Committees { get; init; } = new();

    // Latest salary record
    public SalaryDto? LatestSalary { get; init; }
}

public sealed class VoteStatsDto
{
    public int Total { get; init; }
    public int Yes { get; init; }
    public int No { get; init; }
    public int Abstention { get; init; }
    public int Absent { get; init; }
    public double PresenceRate => Total > 0 ? Math.Round((double)(Yes + No + Abstention) / Total * 100, 1) : 0;
}

public sealed class RecentVoteDto
{
    public int SessionId { get; init; }
    public string VoteValue { get; init; } = string.Empty;
    public DateTime VotingDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string Chamber { get; init; } = string.Empty;
}

public sealed class ExpenseSummaryDto
{
    public int Year { get; init; }
    public decimal Total { get; init; }
    public int Count { get; init; }
    public List<ExpenseCategoryDto> ByCategory { get; init; } = new();
}

public sealed class ExpenseCategoryDto
{
    public string Category { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public int Count { get; init; }
}

// ── Expense DTOs ──────────────────────────────────────────────────────────────

public sealed class ExpenseDto
{
    public int Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Provider { get; init; }
    public string? DocumentNumber { get; init; }
    public DateTime ExpenseDate { get; init; }
    public string? Month { get; init; }
    public int Year { get; init; }
}

// ── Salary DTOs ───────────────────────────────────────────────────────────────

public sealed class SalaryDto
{
    public int Id { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal NetSalary { get; init; }
    public decimal Allowances { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public string? Source { get; init; }
}

// ── Committee DTOs ────────────────────────────────────────────────────────────

public sealed class CommitteeMembershipDto
{
    public int CommitteeId { get; init; }
    public string CommitteeName { get; init; } = string.Empty;
    public string? Acronym { get; init; }
    public string CommitteeType { get; init; } = string.Empty;
    public string Chamber { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public sealed class CommitteeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Acronym { get; init; }
    public string CommitteeType { get; init; } = string.Empty;
    public string Chamber { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int MemberCount { get; init; }
}

// ── Proposal DTOs ─────────────────────────────────────────────────────────────

public class ProposalDto
{
    public int Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? Number { get; init; }
    public int Year { get; init; }
    public string Chamber { get; init; } = string.Empty;
    public string? Author { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ProposalDate { get; init; }
    public int VotingSessionCount { get; init; }
}

public sealed class ProposalDetailDto : ProposalDto
{
    public List<VotingSessionListDto> VotingSessions { get; init; } = new();
}

// ── VotingSession DTOs ────────────────────────────────────────────────────────

public sealed class VotingSessionListDto
{
    public int Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime VotingDate { get; init; }
    public string? SessionType { get; init; }
    public int TotalVotes { get; init; }
    public int VotesYes { get; init; }
    public int VotesNo { get; init; }
    public int VotesAbstention { get; init; }
    public int VotesAbsent { get; init; }
    public string Result { get; init; } = string.Empty;
    public string Chamber { get; init; } = string.Empty;
    public int? ProposalId { get; init; }
    public string? ProposalTitle { get; init; }
    public string? ProposalType { get; init; }
    public bool HasAlert { get; init; }
}

// ── Party DTOs ────────────────────────────────────────────────────────────────

public sealed class PartyDto
{
    public int Id { get; init; }
    public string Acronym { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public int? Number { get; init; }
    public string? President { get; init; }
    public bool IsActive { get; init; }
    public int MemberCount { get; init; }
}

// ── Transparency DTOs ─────────────────────────────────────────────────────────

public sealed class CampaignExpenseDto
{
    public int Id { get; init; }
    public int ElectionYear { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public string? Supplier { get; init; }
    public string? SupplierCnpjCpf { get; init; }
    public string? ExternalId { get; init; }
}

public sealed class AssetDeclarationDto
{
    public int Id { get; init; }
    public int ElectionYear { get; init; }
    public string AssetType { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal DeclaredValue { get; init; }
    public string? ExternalId { get; init; }
}

public sealed class ElectionResultDto
{
    public int Id { get; init; }
    public int ElectionYear { get; init; }
    public string Position { get; init; } = string.Empty;
    public string? State { get; init; }
    public string? City { get; init; }
    public long VotesReceived { get; init; }
    public long TotalVotes { get; init; }
    public decimal VoteShare { get; init; }
    public bool IsElected { get; init; }
    public string? ExternalId { get; init; }
}

public sealed class AttendanceDto
{
    public int Id { get; init; }
    public DateTime SessionDate { get; init; }
    public bool IsPresent { get; init; }
    public string? AbsenceReason { get; init; }
    public string? AbsenceJustification { get; init; }
    public string Chamber { get; init; } = string.Empty;
}

public sealed class CabinetStaffDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Role { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal NetSalary { get; init; }
    public int? Month { get; init; }
    public int? Year { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public sealed class AllowanceDto
{
    public int Id { get; init; }
    public string AllowanceType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public string? Description { get; init; }
    public string? Source { get; init; }
}

public sealed class AllowanceSummaryDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal Total { get; init; }
    public List<AllowanceDto> Items { get; init; } = new();
}

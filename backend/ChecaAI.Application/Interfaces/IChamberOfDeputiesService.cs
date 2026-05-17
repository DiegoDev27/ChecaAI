using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Interfaces;

public interface IChamberOfDeputiesService
{
    Task<IEnumerable<Politician>> GetDeputiesAsync();
    Task<Politician?> GetDeputyByIdAsync(string externalId);
    Task<IEnumerable<Proposal>> GetProposalsAsync(int? year = null, string? type = null);
    Task<Proposal?> GetProposalByIdAsync(string externalId);
    Task<IEnumerable<VotingSession>> GetVotingSessionsByProposalAsync(string proposalId);
    Task<IEnumerable<Vote>> GetVotesByVotingSessionAsync(string votingSessionId);
    Task<IEnumerable<PoliticianExpense>> GetDeputyExpensesAsync(string deputyId, int? year = null, int? month = null);
}
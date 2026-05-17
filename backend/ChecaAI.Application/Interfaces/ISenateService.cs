using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Interfaces;

public interface ISenateService
{
    Task<IEnumerable<Politician>> GetSenatorsAsync();
    Task<Politician?> GetSenatorByIdAsync(string externalId);
    Task<IEnumerable<Proposal>> GetProposalsAsync(int? year = null, string? type = null);
    Task<Proposal?> GetProposalByIdAsync(string externalId);
    Task<IEnumerable<VotingSession>> GetVotingSessionsByProposalAsync(string proposalId);
    Task<IEnumerable<Vote>> GetVotesByVotingSessionAsync(string votingSessionId);
}
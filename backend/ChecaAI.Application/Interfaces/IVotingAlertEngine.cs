using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Interfaces;

public interface IVotingAlertEngine
{
    Task<VotingAlert?> EvaluateAsync(VotingSession session);
}

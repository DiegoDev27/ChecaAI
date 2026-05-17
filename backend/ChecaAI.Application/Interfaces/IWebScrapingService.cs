using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Interfaces;

public interface IWebScrapingService
{
    Task<IEnumerable<Politician>> GetCouncilorsFromCityAsync(string cityName, string stateCode);
    Task<IEnumerable<VotingSession>> GetVotingSessionsFromCityAsync(string cityName, string stateCode);
    Task<IEnumerable<Proposal>> GetProposalsFromCityAsync(string cityName, string stateCode);
    Task<bool> IsCitySupportedAsync(string cityName, string stateCode);
}
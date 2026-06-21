using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Interfaces;

public interface ITseService
{
    Task<IEnumerable<CampaignExpense>> GetCampaignExpensesAsync(string cpf, int electionYear);
    Task<IEnumerable<AssetDeclaration>> GetAssetDeclarationsAsync(string cpf, int electionYear);
    Task<IEnumerable<ElectionResult>> GetElectionResultsAsync(string cpf);
}

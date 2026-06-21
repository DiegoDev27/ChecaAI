using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Interfaces;

public interface ICguService
{
    Task<IEnumerable<PoliticianSalary>> GetPoliticianSalariesAsync(string cpf, int? year = null, int? month = null);
}

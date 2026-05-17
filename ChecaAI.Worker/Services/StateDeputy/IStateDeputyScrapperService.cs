namespace ChecaAI.Worker.Services.StateDeputy;

public interface IStateDeputyScrapperService
{
    string StateCode { get; }      // "RJ", "SP", etc.
    string AssemblyName { get; }   // "ALERJ", "ALESP", etc.
    Task<List<StateDeputyData>> FetchDeputiesAsync();
    Task<bool> IsSourceAvailableAsync();
}

public class StateDeputyData
{
    public string ExternalId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Party { get; set; }
    public string StateCode { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhotoUrl { get; set; }
    public string? ParlamentaryPageUrl { get; set; }
}

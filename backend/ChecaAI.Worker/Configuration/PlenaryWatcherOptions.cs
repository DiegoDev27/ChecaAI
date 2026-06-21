namespace ChecaAI.Worker.Configuration;

public class PlenaryWatcherOptions
{
    public const string SectionName = "PlenaryWatcher";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(90);
    public bool Enabled { get; set; } = true;
    public string CamaraBaseUrl { get; set; } = "https://dadosabertos.camara.leg.br/api/v2";
    public string SenadoBaseUrl { get; set; } = "https://legis.senado.leg.br/dadosabertos";
    public int LookbackHours { get; set; } = 1; // how far back to look for new sessions
}

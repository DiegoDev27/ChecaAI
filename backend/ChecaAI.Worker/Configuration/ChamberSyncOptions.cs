namespace ChecaAI.Worker.Configuration;

public class ChamberSyncOptions
{
    public const string SectionName = "ChamberSync";

    public bool Enabled { get; set; } = true;

    /// <summary>How often to re-sync federal deputies (default every 6 hours).</summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(6);

    public string BaseUrl { get; set; } = "https://dadosabertos.camara.leg.br/api/v2";
}

using Microsoft.AspNetCore.SignalR;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Api.Hubs;

/// <summary>
/// SignalR hub for real-time plenary voting alerts.
/// Clients connect to /hubs/plenary and join the "plenary-alerts" group
/// to receive push notifications when suspicious votes are detected.
/// </summary>
public class PlenaryHub : Hub
{
    private const string AlertsGroup = "plenary-alerts";
    private readonly ILogger<PlenaryHub> _logger;

    public PlenaryHub(ILogger<PlenaryHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects. Auto-joins the plenary-alerts group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AlertsGroup);
        _logger.LogDebug("[PlenaryHub] Client {ConnectionId} connected and joined {Group}",
            Context.ConnectionId, AlertsGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("[PlenaryHub] Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Broadcasts a voting alert to all connected clients in the plenary-alerts group.
    /// Called by the API when a new VotingAlert is detected by the Worker.
    /// </summary>
    public static async Task BroadcastAlertAsync(IHubContext<PlenaryHub> hubContext, VotingAlertPayload payload)
    {
        await hubContext.Clients.Group(AlertsGroup).SendAsync("ReceiveAlert", payload);
    }
}

/// <summary>
/// Lightweight payload sent over SignalR — avoids serializing full EF entities.
/// </summary>
public class VotingAlertPayload
{
    public int AlertId { get; set; }
    public int SessionId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Chamber { get; set; } = string.Empty;
    public string AlertLevel { get; set; } = string.Empty;
    public int Score { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string Description { get; set; } = string.Empty;
}

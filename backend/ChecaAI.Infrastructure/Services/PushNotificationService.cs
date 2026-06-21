using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Infrastructure.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private const string ExpoApiUrl = "https://exp.host/--/api/v2/push/send";

    public PushNotificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PushNotificationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendAlertAsync(VotingAlert alert, VotingSession session)
    {
        // Expo push tokens are stored per-user (future: fetch from DB)
        // For now, uses a configured broadcast token or skips if not configured
        var broadcastToken = _configuration["ExpoNotifications:BroadcastToken"];

        if (string.IsNullOrEmpty(broadcastToken))
        {
            _logger.LogDebug("No Expo broadcast token configured — skipping push notification");
            return false;
        }

        var title = alert.AlertLevel switch
        {
            "Crítico" => "🚨 Votação CRÍTICA detectada",
            "Atenção" => "⚠️ Votação suspeita detectada",
            _ => "ℹ️ Alerta de plenário"
        };

        var chamberName = session.Chamber.Contains("Senado", StringComparison.OrdinalIgnoreCase)
            ? "Senado"
            : "Câmara";

        var body = $"{chamberName}: {session.Description}";
        if (body.Length > 150) body = body[..147] + "...";

        var payload = new
        {
            to = broadcastToken,
            title,
            body,
            data = new
            {
                alertId = alert.Id,
                sessionId = session.Id,
                externalId = session.ExternalId,
                level = alert.AlertLevel,
                score = alert.Score,
                chamber = session.Chamber
            },
            sound = alert.AlertLevel == "Crítico" ? "default" : null as string,
            priority = alert.AlertLevel == "Crítico" ? "high" : "normal",
            channelId = "plenary-alerts"
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.Add("Accept", "application/json");
            content.Headers.Add("Accept-Encoding", "gzip, deflate");

            var response = await _httpClient.PostAsync(ExpoApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Push notification sent for alert {AlertId} (level={Level}, score={Score})",
                    alert.Id, alert.AlertLevel, alert.Score);
                return true;
            }

            _logger.LogWarning(
                "Expo Push returned {StatusCode} for alert {AlertId}: {Body}",
                response.StatusCode, alert.Id, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification for alert {AlertId}", alert.Id);
            return false;
        }
    }
}

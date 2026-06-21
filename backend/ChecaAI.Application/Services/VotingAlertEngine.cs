using System.Text.Json;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Services;

public class VotingAlertEngine : IVotingAlertEngine
{
    private readonly ILogger<VotingAlertEngine> _logger;

    private static readonly string[] ControversialKeywords =
    [
        "urgência", "urgente", "secreto", "sigiloso", "madrugada",
        "reajuste", "salário", "emenda", "contrato emergencial",
        "supersalário", "benefício", "pensão", "contratação direta"
    ];

    public VotingAlertEngine(ILogger<VotingAlertEngine> logger)
    {
        _logger = logger;
    }

    public Task<VotingAlert?> EvaluateAsync(VotingSession session)
    {
        var breakdown = new Dictionary<string, int>();
        var score = 0;

        // 1. Horário da votação (horário de Brasília = UTC-3)
        var brasiliaTime = session.VotingDate.AddHours(-3);
        var hour = brasiliaTime.Hour;

        if (hour >= 23 || hour < 5)
        {
            breakdown["LateNight_23h_5h"] = 40;
            score += 40;
        }
        else if (hour >= 21)
        {
            breakdown["LateEvening_21h_23h"] = 20;
            score += 20;
        }

        // 2. Quórum baixo (< 30% dos votos esperados)
        // Câmara: 513 deputados | Senado: 81 senadores
        var expectedTotal = session.Chamber.Contains("Senado", StringComparison.OrdinalIgnoreCase) ? 81 : 513;
        var quorumPct = session.TotalVotes > 0 ? (double)session.TotalVotes / expectedTotal : 0;
        if (session.TotalVotes > 0 && quorumPct < 0.30)
        {
            breakdown["LowQuorum_Under30pct"] = 20;
            score += 20;
        }

        // 3. Regime de urgência detectado na descrição
        var descLower = (session.Description + " " + (session.Proposal?.Title ?? "")).ToLowerInvariant();
        if (descLower.Contains("urgência") || descLower.Contains("urgente") || descLower.Contains("regime de urgência"))
        {
            breakdown["UrgencyRegime"] = 25;
            score += 25;
        }

        // 4. Palavras-chave polêmicas na ementa
        foreach (var keyword in ControversialKeywords)
        {
            if (descLower.Contains(keyword))
            {
                var key = $"Keyword_{keyword.Replace(" ", "_")}";
                if (!breakdown.ContainsKey(key))
                {
                    breakdown[key] = 10;
                    score += 10;
                }
            }
        }

        // 5. Duração curta — não temos duração diretamente; usamos UpdatedAt - VotingDate como proxy
        // Será avaliado pelo watcher com base em quando a votação foi detectada vs encerrada

        var alertLevel = score switch
        {
            >= 60 => "Crítico",
            >= 35 => "Atenção",
            _ => "Normal"
        };

        _logger.LogDebug(
            "VotingAlertEngine evaluated session {SessionId} ({Chamber}): score={Score}, level={Level}",
            session.ExternalId, session.Chamber, score, alertLevel);

        // Só cria alerta se score > 30 (threshold mínimo para notificar)
        if (score <= 30)
            return Task.FromResult<VotingAlert?>(null);

        var alert = new VotingAlert
        {
            VotingSessionId = session.Id,
            AlertLevel = alertLevel,
            Score = score,
            ScoreBreakdown = JsonSerializer.Serialize(breakdown),
            DetectedAt = DateTime.UtcNow,
            SummaryText = GenerateSummary(session, alertLevel, score, breakdown)
        };

        return Task.FromResult<VotingAlert?>(alert);
    }

    private static string GenerateSummary(VotingSession session, string level, int score, Dictionary<string, int> breakdown)
    {
        var reasons = new List<string>();

        if (breakdown.ContainsKey("LateNight_23h_5h"))
            reasons.Add("votação entre 23h e 5h");
        if (breakdown.ContainsKey("LateEvening_21h_23h"))
            reasons.Add("votação após 21h");
        if (breakdown.ContainsKey("LowQuorum_Under30pct"))
            reasons.Add("quórum abaixo de 30%");
        if (breakdown.ContainsKey("UrgencyRegime"))
            reasons.Add("regime de urgência");

        var keywordReasons = breakdown.Keys
            .Where(k => k.StartsWith("Keyword_"))
            .Select(k => $"palavra-chave \"{k.Replace("Keyword_", "").Replace("_", " ")}\"")
            .ToList();
        reasons.AddRange(keywordReasons);

        var reasonText = reasons.Any() ? string.Join(", ", reasons) : "múltiplos critérios";
        var chamberName = session.Chamber.Contains("Senado", StringComparison.OrdinalIgnoreCase) ? "Senado" : "Câmara";

        return $"⚠️ [{level.ToUpperInvariant()}] Votação suspeita detectada no {chamberName} " +
               $"(score: {score}): {reasonText}. " +
               $"Proposta: {session.Description.Truncate(100)}";
    }
}

file static class StringExtensions
{
    public static string Truncate(this string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}

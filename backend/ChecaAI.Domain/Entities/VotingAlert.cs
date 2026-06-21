using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class VotingAlert
{
    public int Id { get; set; }

    public int VotingSessionId { get; set; }

    [Required]
    [MaxLength(20)]
    public string AlertLevel { get; set; } = string.Empty; // Normal, Atenção, Crítico

    public int Score { get; set; }

    // JSON breakdown: {"LateNight":40,"ShortDuration":35,...}
    [MaxLength(500)]
    public string? ScoreBreakdown { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? SummaryText { get; set; }

    public bool SignalRSent { get; set; } = false;

    public bool PushSent { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual VotingSession VotingSession { get; set; } = null!;
}

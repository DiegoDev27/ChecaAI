using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChecaAI.Domain.Entities;

public class ElectionResult
{
    public int Id { get; set; }

    public int PoliticianId { get; set; }

    public int ElectionYear { get; set; }

    [Required]
    [MaxLength(50)]
    public string Position { get; set; } = string.Empty; // Senador, Deputado Federal, Vereador, etc

    [MaxLength(2)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public long VotesReceived { get; set; }

    public long TotalVotes { get; set; }

    [Column(TypeName = "decimal(8,4)")]
    public decimal VoteShare { get; set; } // percentage

    public bool IsElected { get; set; }

    [MaxLength(50)]
    public string? ExternalId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Politician Politician { get; set; } = null!;
}

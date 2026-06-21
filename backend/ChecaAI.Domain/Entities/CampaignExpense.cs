using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChecaAI.Domain.Entities;

public class CampaignExpense
{
    public int Id { get; set; }

    public int PoliticianId { get; set; }

    public int ElectionYear { get; set; }

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty; // Publicidade, Combustível, Pessoal, etc

    [Column(TypeName = "decimal(15,2)")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Provider { get; set; }

    [MaxLength(20)]
    public string? ProviderCnpj { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? ExternalId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Politician Politician { get; set; } = null!;
}

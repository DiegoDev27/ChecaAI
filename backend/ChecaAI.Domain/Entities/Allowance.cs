using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChecaAI.Domain.Entities;

/// <summary>
/// Auxílios pagos ao parlamentar (moradia, saúde, educação, transporte, etc.).
/// Fonte: Portal da Transparência / CGU.
/// </summary>
public class Allowance
{
    public int Id { get; set; }

    public int PoliticianId { get; set; }

    [Required]
    [MaxLength(100)]
    public string AllowanceType { get; set; } = string.Empty;
    // Moradia, Saúde, Educação, Transporte, Alimentação, Pré-Escolar, Natalidade

    [Column(TypeName = "decimal(15,2)")]
    public decimal Amount { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Source { get; set; } // CGU, Portal da Câmara, etc.

    [MaxLength(100)]
    public string? ExternalId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Politician Politician { get; set; } = null!;
}

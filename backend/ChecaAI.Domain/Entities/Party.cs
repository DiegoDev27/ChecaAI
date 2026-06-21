using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

/// <summary>
/// Partido político com dados completos (sigla, número TSE, presidente, etc.).
/// Fonte: TSE (dadosabertos.tse.jus.br) + Câmara (/partidos).
/// </summary>
public class Party
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string Acronym { get; set; } = string.Empty; // Ex: "PT", "PL", "PSDB"

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty; // Ex: "Partido dos Trabalhadores"

    public int? Number { get; set; } // Número eleitoral TSE, ex: 13

    public DateOnly? FoundedDate { get; set; }

    [MaxLength(200)]
    public string? President { get; set; }

    [MaxLength(100)]
    public string? ExternalId { get; set; } // ID na Câmara ou TSE

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Politician> Politicians { get; set; } = new List<Politician>();
}

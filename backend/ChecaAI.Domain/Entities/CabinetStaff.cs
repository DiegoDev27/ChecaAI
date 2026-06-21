using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChecaAI.Domain.Entities;

/// <summary>
/// Assessores e servidores de gabinete de um parlamentar.
/// Fonte: Portal da Transparência CGU (/api-de-dados/servidores).
/// </summary>
public class CabinetStaff
{
    public int Id { get; set; }

    public int? PoliticianId { get; set; }

    [Required]
    [MaxLength(300)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Role { get; set; } // Assessor Parlamentar, Chefe de Gabinete, Secretário, etc.

    [Column(TypeName = "decimal(15,2)")]
    public decimal GrossSalary { get; set; }

    [Column(TypeName = "decimal(15,2)")]
    public decimal NetSalary { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(14)]
    public string? Cpf { get; set; }

    [MaxLength(100)]
    public string? ExternalId { get; set; }

    [MaxLength(50)]
    public string? Source { get; set; } // CGU, Portal da Câmara, etc.

    public int? Month { get; set; }

    public int? Year { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Politician? Politician { get; set; }
}

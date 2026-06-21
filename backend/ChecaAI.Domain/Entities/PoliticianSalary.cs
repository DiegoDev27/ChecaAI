using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChecaAI.Domain.Entities;

public class PoliticianSalary
{
    public int Id { get; set; }

    public int PoliticianId { get; set; }

    [Column(TypeName = "decimal(15,2)")]
    public decimal GrossSalary { get; set; }

    [Column(TypeName = "decimal(15,2)")]
    public decimal NetSalary { get; set; }

    [Column(TypeName = "decimal(15,2)")]
    public decimal Allowances { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; } // CGU, etc

    [MaxLength(50)]
    public string? ExternalId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Politician Politician { get; set; } = null!;
}

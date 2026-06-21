using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class Committee
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string? ExternalId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Acronym { get; set; }

    [Required]
    [MaxLength(50)]
    public string CommitteeType { get; set; } = string.Empty; // Permanente, CPI, Especial, Mista

    [Required]
    [MaxLength(50)]
    public string Chamber { get; set; } = string.Empty; // Câmara, Senado, Mista

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<CommitteeMembership> Members { get; set; } = new List<CommitteeMembership>();
}

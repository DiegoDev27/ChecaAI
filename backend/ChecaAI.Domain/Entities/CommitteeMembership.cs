using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class CommitteeMembership
{
    public int Id { get; set; }

    public int CommitteeId { get; set; }

    public int PoliticianId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty; // Presidente, VicePresidente, Titular, Suplente

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Committee Committee { get; set; } = null!;
    public virtual Politician Politician { get; set; } = null!;
}

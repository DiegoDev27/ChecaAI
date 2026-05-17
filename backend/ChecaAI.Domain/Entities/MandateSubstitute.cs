using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class MandateSubstitute
{
    public int Id { get; set; }
    
    [Required]
    public int MandateId { get; set; }
    
    [Required]
    public int SubstitutePoliticianId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string ParticipationDescription { get; set; } = string.Empty; // "1º Suplente", "2º Suplente"
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual PoliticianMandate Mandate { get; set; } = null!;
    public virtual Politician SubstitutePolitician { get; set; } = null!;
}
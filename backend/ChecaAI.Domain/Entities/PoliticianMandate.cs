using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class PoliticianMandate
{
    public int Id { get; set; }
    
    [Required]
    public int PoliticianId { get; set; }
    
    public int MandateCode { get; set; }
    
    [Required]
    [MaxLength(2)]
    public string State { get; set; } = string.Empty; // UF
    
    [Required]
    [MaxLength(100)]
    public string ParticipationDescription { get; set; } = string.Empty; // Titular, 1º Suplente, etc.
    
    // For substitutes, reference to the titular politician
    public int? TitularPoliticianId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Politician Politician { get; set; } = null!;
    public virtual Politician? TitularPolitician { get; set; }
    public virtual ICollection<Legislature> Legislatures { get; set; } = new List<Legislature>();
    public virtual ICollection<MandateSubstitute> Substitutes { get; set; } = new List<MandateSubstitute>();
    public virtual ICollection<MandateExercise> Exercises { get; set; } = new List<MandateExercise>();
}
using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class Legislature
{
    public int Id { get; set; }
    
    [Required]
    public int MandateId { get; set; }
    
    public int Number { get; set; }
    
    public DateOnly StartDate { get; set; }
    
    public DateOnly EndDate { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string LegislatureType { get; set; } = string.Empty; // "Primeira" or "Segunda"
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual PoliticianMandate Mandate { get; set; } = null!;
}
using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class PoliticianPhone
{
    public int Id { get; set; }
    
    [Required]
    public int PoliticianId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    public int PublicationOrder { get; set; }
    
    public bool IsFax { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Politician Politician { get; set; } = null!;
}
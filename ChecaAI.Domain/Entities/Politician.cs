using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class Politician
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Cpf { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string PoliticalPosition { get; set; } = string.Empty; // Federal Deputy, Senator, City Councilor, etc
    
    [MaxLength(100)]
    public string? Party { get; set; }
    
    [MaxLength(2)]
    public string? State { get; set; } // UF
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(20)]
    public string? ExternalId { get; set; } // ID from government APIs
    
    [MaxLength(500)]
    public string? PhotoUrl { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public virtual ICollection<PoliticianExpense> Expenses { get; set; } = new List<PoliticianExpense>();
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChecaAI.Domain.Entities;

public class PoliticianExpense
{
    public int Id { get; set; }
    
    public int PoliticianId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty; // Fuel, Lodging, Food, etc
    
    [Column(TypeName = "decimal(15,2)")]
    public decimal Amount { get; set; }
    
    [MaxLength(100)]
    public string? Provider { get; set; }
    
    [MaxLength(50)]
    public string? DocumentNumber { get; set; }
    
    public DateTime ExpenseDate { get; set; }
    
    [MaxLength(50)]
    public string? Month { get; set; }
    
    public int Year { get; set; }
    
    [MaxLength(20)]
    public string? ExternalId { get; set; } // ID from government APIs
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Politician Politician { get; set; } = null!;
}
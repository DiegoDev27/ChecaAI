using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class PoliticalBloc
{
    public int Id { get; set; }
    
    public int Code { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Nickname { get; set; }
    
    public DateOnly CreationDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<Politician> Politicians { get; set; } = new List<Politician>();
}
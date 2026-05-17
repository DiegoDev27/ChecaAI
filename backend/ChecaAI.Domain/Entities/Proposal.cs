using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class Proposal
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string ExternalId { get; set; } = string.Empty; // ID from government APIs
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // PL, PEC, MP, etc
    
    [MaxLength(20)]
    public string? Number { get; set; }
    
    public int Year { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Chamber { get; set; } = string.Empty; // Chamber of Deputies, Federal Senate, etc
    
    [MaxLength(100)]
    public string? Author { get; set; }
    
    [MaxLength(500)]
    public string? Summary { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty; // In Process, Approved, Rejected, etc
    
    public DateTime? ProposalDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<VotingSession> VotingSessions { get; set; } = new List<VotingSession>();
}
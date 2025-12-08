using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class Vote
{
    public int Id { get; set; }
    
    public int PoliticianId { get; set; }
    public int VotingSessionId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string VoteValue { get; set; } = string.Empty; // Yes, No, Abstention, Absent
    
    [MaxLength(200)]
    public string? Justification { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Politician Politician { get; set; } = null!;
    public virtual VotingSession VotingSession { get; set; } = null!;
}
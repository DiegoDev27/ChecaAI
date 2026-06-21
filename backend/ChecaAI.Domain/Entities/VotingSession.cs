using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class VotingSession
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ExternalId { get; set; } = string.Empty; // ID from government APIs
    
    public int ProposalId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;
    
    public DateTime VotingDate { get; set; }
    
    [MaxLength(50)]
    public string? SessionType { get; set; } // Nominal, Symbolic, Secret
    
    public int TotalVotes { get; set; }
    public int VotesYes { get; set; }
    public int VotesNo { get; set; }
    public int VotesAbstention { get; set; }
    public int VotesAbsent { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Result { get; set; } = string.Empty; // Approved, Rejected, Postponed
    
    [MaxLength(50)]
    public string Chamber { get; set; } = string.Empty; // Chamber of Deputies, Federal Senate, etc
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Proposal Proposal { get; set; } = null!;
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public virtual ICollection<VotingAlert> Alerts { get; set; } = new List<VotingAlert>();
}
using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class MandateExercise
{
    public int Id { get; set; }
    
    [Required]
    public int MandateId { get; set; }
    
    public int ExerciseCode { get; set; }
    
    public DateOnly StartDate { get; set; }
    
    public DateOnly? EndDate { get; set; }
    
    [MaxLength(10)]
    public string? LeaveReasonCode { get; set; } // AFO, LCS, LP, RET, etc.
    
    [MaxLength(200)]
    public string? LeaveReasonDescription { get; set; }
    
    public DateOnly? ReadingDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual PoliticianMandate Mandate { get; set; } = null!;
}
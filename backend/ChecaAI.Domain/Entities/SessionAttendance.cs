using System.ComponentModel.DataAnnotations;

namespace ChecaAI.Domain.Entities;

public class SessionAttendance
{
    public int Id { get; set; }

    public int PoliticianId { get; set; }

    public DateTime SessionDate { get; set; }

    public bool IsPresent { get; set; }

    [MaxLength(100)]
    public string? AbsenceReason { get; set; } // Missão Oficial, Licença Médica, etc

    [MaxLength(500)]
    public string? AbsenceJustification { get; set; }

    [Required]
    [MaxLength(50)]
    public string Chamber { get; set; } = string.Empty; // Câmara, Senado, Municipal

    [MaxLength(50)]
    public string? ExternalId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Politician Politician { get; set; } = null!;
}

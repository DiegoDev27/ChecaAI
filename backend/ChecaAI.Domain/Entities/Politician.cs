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
    
    [MaxLength(50)]
    public string? ExternalId { get; set; } // ID from government APIs (CodigoParlamentar for Senate)
    
    [MaxLength(500)]
    public string? PhotoUrl { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Senate-specific fields
    [MaxLength(20)]
    public string? CurrentLegislaturePublicCode { get; set; } // CodigoPublicoNaLegAtual
    
    [MaxLength(100)]
    public string? ParlamentaryName { get; set; } // NomeParlamentar
    
    [MaxLength(20)]
    public string? Gender { get; set; } // Masculino/Feminino
    
    [MaxLength(50)]
    public string? Treatment { get; set; } // Senador/Senadora
    
    [MaxLength(500)]
    public string? ParlamentaryPageUrl { get; set; } // UrlPaginaParlamentar
    
    [MaxLength(500)]
    public string? PersonalPageUrl { get; set; } // UrlPaginaParticular
    
    [MaxLength(100)]
    public string? Email { get; set; } // EmailParlamentar
    
    public bool IsBoardMember { get; set; } = false; // MembroMesa
    public bool IsLeadershipMember { get; set; } = false; // MembroLideranca
    
    // Foreign Keys
    public int? PoliticalBlocId { get; set; }
    public int? PartyId { get; set; } // FK → Party (rich entity; Party string field kept as fallback)

    // Navigation properties
    public virtual PoliticalBloc? PoliticalBloc { get; set; }
    public virtual Party? PartyEntity { get; set; }
    public virtual ICollection<PoliticianPhone> Phones { get; set; } = new List<PoliticianPhone>();
    public virtual ICollection<PoliticianMandate> Mandates { get; set; } = new List<PoliticianMandate>();
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public virtual ICollection<PoliticianExpense> Expenses { get; set; } = new List<PoliticianExpense>();
}
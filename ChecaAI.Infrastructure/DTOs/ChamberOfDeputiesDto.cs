using System.Text.Json.Serialization;

namespace ChecaAI.Infrastructure.DTOs;

public class ChamberOfDeputiesResponse<T>
{
    [JsonPropertyName("dados")]
    public List<T> Data { get; set; } = new();
    
    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = new();
}

public class Link
{
    [JsonPropertyName("rel")]
    public string Rel { get; set; } = string.Empty;
    
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}

public class DeputyDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("nome")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("siglaPartido")]
    public string Party { get; set; } = string.Empty;
    
    [JsonPropertyName("siglaUf")]
    public string State { get; set; } = string.Empty;
    
    [JsonPropertyName("urlFoto")]
    public string? PhotoUrl { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("cpf")]
    public string? Cpf { get; set; }
}

public class ProposalDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("siglaTipo")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("numero")]
    public int Number { get; set; }
    
    [JsonPropertyName("ano")]
    public int Year { get; set; }
    
    [JsonPropertyName("ementa")]
    public string Summary { get; set; } = string.Empty;
    
    [JsonPropertyName("dataApresentacao")]
    public DateTime? PresentationDate { get; set; }
    
    [JsonPropertyName("statusProposicao")]
    public ProposalStatusDto? Status { get; set; }
}

public class ProposalStatusDto
{
    [JsonPropertyName("descricaoSituacao")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("descricaoTramitacao")]
    public string ProcessingDescription { get; set; } = string.Empty;
}

public class VotingSessionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("descricao")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public DateTime Date { get; set; }
    
    [JsonPropertyName("aprovacao")]
    public int Approval { get; set; }
    
    [JsonPropertyName("dataHoraRegistro")]
    public DateTime? RegistrationDateTime { get; set; }
}

public class VoteDto
{
    [JsonPropertyName("deputado_")]
    public DeputyVoteDto Deputy { get; set; } = new();
    
    [JsonPropertyName("tipoVoto")]
    public string VoteType { get; set; } = string.Empty;
}

public class DeputyVoteDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("nome")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("siglaPartido")]
    public string Party { get; set; } = string.Empty;
    
    [JsonPropertyName("siglaUf")]
    public string State { get; set; } = string.Empty;
}

public class ExpenseDto
{
    [JsonPropertyName("ano")]
    public int Year { get; set; }
    
    [JsonPropertyName("mes")]
    public int Month { get; set; }
    
    [JsonPropertyName("tipoDespesa")]
    public string ExpenseType { get; set; } = string.Empty;
    
    [JsonPropertyName("codDocumento")]
    public int DocumentCode { get; set; }
    
    [JsonPropertyName("dataDocumento")]
    public DateTime DocumentDate { get; set; }
    
    [JsonPropertyName("numDocumento")]
    public string DocumentNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("valorDocumento")]
    public decimal DocumentValue { get; set; }
    
    [JsonPropertyName("urlDocumento")]
    public string? DocumentUrl { get; set; }
    
    [JsonPropertyName("nomeFornecedor")]
    public string ProviderName { get; set; } = string.Empty;
    
    [JsonPropertyName("cnpjCpfFornecedor")]
    public string? ProviderDocument { get; set; }
}
using System.Text.Json.Serialization;

namespace ChecaAI.Worker.Models.DTOs;

public class ParlamentarDto
{
    [JsonPropertyName("IdentificacaoParlamentar")]
    public IdentificacaoParlamentarDto IdentificacaoParlamentar { get; set; } = new();
    
    [JsonPropertyName("Mandato")]
    public MandatoDto Mandato { get; set; } = new();
}

public class IdentificacaoParlamentarDto
{
    [JsonPropertyName("CodigoParlamentar")]
    public string CodigoParlamentar { get; set; } = string.Empty;
    
    [JsonPropertyName("CodigoPublicoNaLegAtual")]
    public string CodigoPublicoNaLegAtual { get; set; } = string.Empty;
    
    [JsonPropertyName("NomeParlamentar")]
    public string NomeParlamentar { get; set; } = string.Empty;
    
    [JsonPropertyName("NomeCompletoParlamentar")]
    public string NomeCompletoParlamentar { get; set; } = string.Empty;
    
    [JsonPropertyName("SexoParlamentar")]
    public string SexoParlamentar { get; set; } = string.Empty;
    
    [JsonPropertyName("FormaTratamento")]
    public string FormaTratamento { get; set; } = string.Empty;
    
    [JsonPropertyName("UrlFotoParlamentar")]
    public string? UrlFotoParlamentar { get; set; }
    
    [JsonPropertyName("UrlPaginaParlamentar")]
    public string? UrlPaginaParlamentar { get; set; }
    
    [JsonPropertyName("UrlPaginaParticular")]
    public string? UrlPaginaParticular { get; set; }
    
    [JsonPropertyName("EmailParlamentar")]
    public string? EmailParlamentar { get; set; }
    
    [JsonPropertyName("SiglaPartidoParlamentar")]
    public string SiglaPartidoParlamentar { get; set; } = string.Empty;
    
    [JsonPropertyName("UfParlamentar")]
    public string UfParlamentar { get; set; } = string.Empty;
    
    [JsonPropertyName("MembroMesa")]
    public string MembroMesa { get; set; } = string.Empty;
    
    [JsonPropertyName("MembroLideranca")]
    public string MembroLideranca { get; set; } = string.Empty;
    
    [JsonPropertyName("Telefones")]
    public TelefonesDto? Telefones { get; set; }
    
    [JsonPropertyName("Bloco")]
    public BlocoDto? Bloco { get; set; }
}

public class TelefonesDto
{
    [JsonPropertyName("Telefone")]
    public List<TelefoneDto> Telefone { get; set; } = new();
}

public class TelefoneDto
{
    [JsonPropertyName("NumeroTelefone")]
    public string NumeroTelefone { get; set; } = string.Empty;
    
    [JsonPropertyName("OrdemPublicacao")]
    public string OrdemPublicacao { get; set; } = string.Empty;
    
    [JsonPropertyName("IndicadorFax")]
    public string IndicadorFax { get; set; } = string.Empty;
}

public class BlocoDto
{
    [JsonPropertyName("CodigoBloco")]
    public string CodigoBloco { get; set; } = string.Empty;
    
    [JsonPropertyName("NomeBloco")]
    public string NomeBloco { get; set; } = string.Empty;
    
    [JsonPropertyName("NomeApelido")]
    public string? NomeApelido { get; set; }
    
    [JsonPropertyName("DataCriacao")]
    public string? DataCriacao { get; set; }
}
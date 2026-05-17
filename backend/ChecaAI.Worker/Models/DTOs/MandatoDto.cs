using System.Text.Json.Serialization;

namespace ChecaAI.Worker.Models.DTOs;

public class MandatoDto
{
    [JsonPropertyName("CodigoMandato")]
    public string CodigoMandato { get; set; } = string.Empty;
    
    [JsonPropertyName("UfParlamentar")]
    public string UfParlamentar { get; set; } = string.Empty;
    
    [JsonPropertyName("DescricaoParticipacao")]
    public string DescricaoParticipacao { get; set; } = string.Empty;
    
    [JsonPropertyName("PrimeiraLegislaturaDoMandato")]
    public LegislaturaDto? PrimeiraLegislaturaDoMandato { get; set; }
    
    [JsonPropertyName("SegundaLegislaturaDoMandato")]
    public LegislaturaDto? SegundaLegislaturaDoMandato { get; set; }
    
    [JsonPropertyName("Exercicios")]
    public ExerciciosDto? Exercicios { get; set; }
    
    [JsonPropertyName("Suplentes")]
    public SuplentesDto? Suplentes { get; set; }
}

public class LegislaturaDto
{
    [JsonPropertyName("NumeroLegislatura")]
    public string NumeroLegislatura { get; set; } = string.Empty;
    
    [JsonPropertyName("DataInicio")]
    public string? DataInicio { get; set; }
    
    [JsonPropertyName("DataFim")]
    public string? DataFim { get; set; }
}

public class ExerciciosDto
{
    [JsonPropertyName("Exercicio")]
    public List<ExercicioDto> Exercicio { get; set; } = new();
}

public class ExercicioDto
{
    [JsonPropertyName("CodigoExercicio")]
    public string CodigoExercicio { get; set; } = string.Empty;
    
    [JsonPropertyName("DataInicio")]
    public string? DataInicio { get; set; }
    
    [JsonPropertyName("DataFim")]
    public string? DataFim { get; set; }
    
    [JsonPropertyName("SiglaCausaAfastamento")]
    public string? SiglaCausaAfastamento { get; set; }
    
    [JsonPropertyName("DescricaoCausaAfastamento")]
    public string? DescricaoCausaAfastamento { get; set; }
    
    [JsonPropertyName("DataLeitura")]
    public string? DataLeitura { get; set; }
}

public class SuplentesDto
{
    [JsonPropertyName("Suplente")]
    public List<SuplenteDto> Suplente { get; set; } = new();
}

public class SuplenteDto
{
    [JsonPropertyName("CodigoSuplente")]
    public string CodigoSuplente { get; set; } = string.Empty;
    
    [JsonPropertyName("NomeSuplente")]
    public string NomeSuplente { get; set; } = string.Empty;
    
    [JsonPropertyName("DescricaoParticipacao")]
    public string DescricaoParticipacao { get; set; } = string.Empty;
}
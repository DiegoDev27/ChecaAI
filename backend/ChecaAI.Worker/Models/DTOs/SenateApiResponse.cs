using System.Text.Json.Serialization;

namespace ChecaAI.Worker.Models.DTOs;

public class SenateApiResponse
{
    [JsonPropertyName("ListaParlamentarEmExercicio")]
    public ListaParlamentarEmExercicioDto ListaParlamentarEmExercicio { get; set; } = new();
}

public class ListaParlamentarEmExercicioDto
{
    [JsonPropertyName("Metadados")]
    public MetadadosDto Metadados { get; set; } = new();
    
    [JsonPropertyName("Parlamentares")]
    public ParlamentaresDto Parlamentares { get; set; } = new();
}

public class ParlamentaresDto
{
    [JsonPropertyName("Parlamentar")]
    public List<ParlamentarDto> Parlamentar { get; set; } = new();
}

public class MetadadosDto
{
    [JsonPropertyName("Versao")]
    public string Versao { get; set; } = string.Empty;
    
    [JsonPropertyName("VersaoServico")]
    public string VersaoServico { get; set; } = string.Empty;
    
    [JsonPropertyName("DescricaoDataSet")]
    public string DescricaoDataSet { get; set; } = string.Empty;
}
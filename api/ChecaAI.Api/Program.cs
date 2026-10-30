using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

// Primeiro endpoint real: o modelo hierárquico do Estado (Categoria → Domínio → Instituição → Órgão → Cargo).
// Enquanto não existe o PostgreSQL de servir, ele é lido do JSON versionado em modelo/.
var caminhoModelo = Path.GetFullPath(
    builder.Configuration["Modelo:Caminho"] ?? Path.Combine("..", "..", "modelo", "estado-brasileiro.json"),
    builder.Environment.ContentRootPath);

app.MapGet("/v1/estado", async () =>
{
    if (!File.Exists(caminhoModelo))
        return Results.Problem($"Modelo não encontrado em {caminhoModelo}", statusCode: 500);

    await using var arquivo = File.OpenRead(caminhoModelo);
    return Results.Ok(await JsonNode.ParseAsync(arquivo));
})
.WithName("GetModeloEstado");

app.Run();

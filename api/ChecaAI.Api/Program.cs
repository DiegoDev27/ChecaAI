using System.Text.Json.Nodes;
using ChecaAI.Api;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Servir")
    ?? throw new InvalidOperationException("ConnectionStrings:Servir não configurada."));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().WithMethods("GET")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

// O modelo hierárquico do Estado (Categoria → Domínio → Instituição → Órgão → Cargo).
// Enquanto ele não vai para o PostgreSQL de servir, é lido do JSON versionado em modelo/.
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

app.MapEmpresas();

app.Run();

using System.Text.Json.Nodes;
using Dapper;
using Npgsql;

namespace ChecaAI.Api;

public static class Empresas
{
    public static void MapEmpresas(this IEndpointRouteBuilder app)
    {
        // {**cnpj} aceita a barra do CNPJ formatado (12.345.678/0001-90).
        app.MapGet("/v1/empresas/{**cnpj}", ObterPorCnpj)
            .WithName("GetEmpresa")
            .WithSummary("Cadastro da empresa na Receita, seus sócios e as empresas das quais ela é sócia.");
    }

    // Aceita CNPJ com ou sem máscara, com 14 dígitos ou só a raiz (8).
    private static async Task<IResult> ObterPorCnpj(string cnpj, NpgsqlDataSource db)
    {
        // Decodifica antes de filtrar: senão o "2" de um "%2F" vira dígito do CNPJ.
        var digitos = new string(Uri.UnescapeDataString(cnpj).Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length is not (8 or 14))
            return Results.BadRequest(new { erro = "Informe um CNPJ com 14 dígitos ou a raiz com 8." });

        var id = $"empresa:{digitos[..8]}";
        await using var conexao = await db.OpenConnectionAsync();

        var empresa = await conexao.QuerySingleOrDefaultAsync<EntidadeLinha>(
            """
            SELECT id, tipo, nome, documento, atributos::text AS atributos, fonte, referencia
            FROM ouro.entidade WHERE id = @id
            """, new { id });

        if (empresa is null)
            return Results.NotFound(new { erro = $"CNPJ {digitos} não encontrado na base carregada." });

        var socios = await conexao.QueryAsync<VinculoLinha>(
            """
            SELECT e.id, e.tipo, e.nome, e.documento, r.atributos::text AS atributos
            FROM ouro.relacao r JOIN ouro.entidade e ON e.id = r.origem_id
            WHERE r.destino_id = @id AND r.tipo = 'socio_de'
            ORDER BY e.nome
            """, new { id });

        var participacoes = await conexao.QueryAsync<VinculoLinha>(
            """
            SELECT e.id, e.tipo, e.nome, e.documento, r.atributos::text AS atributos
            FROM ouro.relacao r JOIN ouro.entidade e ON e.id = r.destino_id
            WHERE r.origem_id = @id AND r.tipo = 'socio_de'
            ORDER BY e.nome
            """, new { id });

        return Results.Ok(new
        {
            empresa.Id,
            empresa.Nome,
            Cnpj = empresa.Documento,
            Atributos = JsonNode.Parse(empresa.Atributos),
            Socios = socios.Select(ParaVinculo),
            SocioDe = participacoes.Select(ParaVinculo),
            Fonte = new
            {
                Id = empresa.Fonte,
                Nome = "Receita Federal — Dados Abertos do CNPJ",
                Referencia = empresa.Referencia,
                Url = "https://arquivos.receitafederal.gov.br/index.php/s/YggdBLfdninEJX9",
            },
        });
    }

    private static object ParaVinculo(VinculoLinha v) => new
    {
        v.Id,
        v.Tipo,
        v.Nome,
        v.Documento,
        Atributos = JsonNode.Parse(v.Atributos),
    };

    private sealed record EntidadeLinha(string Id, string Tipo, string? Nome, string? Documento, string Atributos, string Fonte, string Referencia);

    private sealed record VinculoLinha(string Id, string Tipo, string? Nome, string? Documento, string Atributos);
}

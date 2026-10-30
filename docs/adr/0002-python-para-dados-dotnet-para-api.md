# ADR-0002: Python (e SQL) para os dados, .NET para a API

- Status: aceita
- Data: 2026-10-29

## Contexto
O sistema tem duas cargas de trabalho de natureza diferente:
1. **Pipeline de dados (lote):** baixar arquivos grandes (CNPJ, TSE e DataJud chegam a dezenas de GB), limpar, cruzar e resolver entidades. O ecossistema maduro para isso é Python e SQL analítico: pandas e polars, DuckDB, dbt e bibliotecas de *record linkage*.
2. **Servir (online):** uma API de leitura, com cache, paginação, rate limit e OpenAPI. É onde o autor tem mais experiência (C# e ASP.NET Core) e onde a carreira dele está (sênior .NET + IA).

## Decisão
- **Ingestão** em scripts Python simples, um por fonte, que gravam Parquet (camada bronze).
- **Transformação** em SQL com dbt sobre o DuckDB (camadas prata e ouro), publicando no PostgreSQL de servir.
- **API** em ASP.NET Core (.NET 10), só de leitura, lendo o PostgreSQL com Dapper ou EF Core sem tracking.
- O **contrato entre os dois mundos é o schema ouro no PostgreSQL**. Nenhum lado chama o outro diretamente.

## Alternativas consideradas
- **Tudo em .NET.** Seria uma linguagem só, mas o ecossistema de dados em .NET é fraco (não há dbt, e DuckDB e Arrow são cidadãos de segunda classe), e era o que tornava os workers da v0 longos.
- **Tudo em Python (FastAPI).** Seria uma linguagem só, mas desperdiça a maior força do autor e o objetivo de carreira.

## Consequências
- (+) Cada parte usa a ferramenta certa, e o pipeline pode rodar sem a API (e vice-versa).
- (+) Fica fácil trocar ou escalar um lado sem mexer no outro.
- (−) Duas linguagens no repositório e duas toolchains no CI.

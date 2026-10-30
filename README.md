# ChecaAI

Banco unificado e relacionado dos dados públicos do Estado brasileiro (o que o cidadão pode consultar pela LAI), com cruzamento entre pessoas, órgãos, empresas e contratos, uma API aberta e uma interface simples para qualquer pessoa. Apartidário: mostra fatos e fontes, nunca vereditos.

> A v0 (TCC) está arquivada em `DiegoDev27/ChecaAI_TCC`. Este repositório recomeça a partir do modelo hierárquico do Estado, reaproveitando o conhecimento das fontes. Veja a [ADR-0001](docs/adr/0001-recomecar-hibrido.md).

## Princípios
1. **O banco é o produto.** A IA é uma camada substituível, e toda resposta cita a fonte.
2. **Resolução de entidades** é o diferencial: ligar político ↔ doador ↔ empresa ↔ contrato.
3. **Nada sigiloso, LGPD respeitada.** Só dados públicos, sem desmascarar CPF.

## Estrutura
| Pasta | O quê | Stack |
|---|---|---|
| `catalogo/` | Inventário das fontes de dados (o "censo" das fontes) | YAML |
| `modelo/` | Modelo hierárquico do Estado (Categoria → Domínio → Instituição → Órgão → Cargo) | JSON |
| `ingestao/` | Coletores: baixam as fontes e gravam a camada bronze (Parquet bruto) | Python |
| `transformacao/` | Camadas prata e ouro: limpeza, resolução de entidades, grafo | DuckDB + dbt (SQL) |
| `api/` | API de leitura sobre o PostgreSQL de servir | ASP.NET Core (.NET 10) |
| `web/` | Site público, uma página por pessoa, órgão e empresa | Next.js |
| `docs/adr/` | Decisões de arquitetura | Markdown |

## Fluxo de dados
```
fontes (APIs, CSV, ZIP, HTML)
  → ingestao/ (Python)        → bronze: Parquet bruto, particionado por fonte e data
  → transformacao/ (dbt)      → prata: limpo e tipado · ouro: entidades + relações
  → PostgreSQL (servir)       → tabelas entidade/relacao, pg_trgm, pgvector
  → api/ (.NET)               → JSON público
  → web/ (Next.js)            → páginas para pessoas e buscadores
```

## Como rodar (walking skeleton do CNPJ)
Pré-requisitos: [uv](https://docs.astral.sh/uv/) (Python), Node.js LTS, .NET 10 SDK e Docker Desktop.

```bash
# 0. Banco de servir (PostgreSQL 17 na porta 5442)
docker compose up -d

# 1. Bronze: baixa a fatia 1 do CNPJ do mês mais recente (~450 MB) e grava Parquet em dados/
cd ingestao && uv run python -m checaai_ingestao.cli receita-cnpj   # --fatias 0-9 para a base inteira

# 2. Prata e ouro: modelos e testes do dbt (dados/checaai.duckdb)
cd ../transformacao && uv run dbt build

# 3. Publica a camada ouro no PostgreSQL (troca atômica de schema)
uv run python scripts/publicar.py

# 4. API em http://localhost:5179  →  GET /v1/empresas/{cnpj}
cd ../api/ChecaAI.Api && dotnet run --launch-profile http

# 5. Site em http://localhost:3000  →  /empresa/{cnpj}
cd ../../web && npm install && npm run dev
```

No Windows, rode o coletor com `python -m` (como acima): o Controle de Aplicativo do Windows bloqueia o `.exe` que o uv gera para `checaai-coletar`.

## Ondas de ingestão
1. Políticos (Câmara, Senado, TSE, CGU), migrando o conhecimento da v0.
2. CNPJ (Receita), sanções CEIS/CNEP e contratos públicos.
3. DataJud (CNJ).
4. CNES/DataSUS.

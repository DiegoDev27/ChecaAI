# transformacao

Projeto dbt sobre o DuckDB. Lê a camada bronze (Parquet) e produz:
- **prata**: tabelas limpas e tipadas, uma por fonte;
- **ouro**: o grafo `entidade` / `relacao` (pessoa, órgão, empresa, contrato…), publicado no PostgreSQL de servir.

Será inicializado (`dbt init`) junto com o walking skeleton do CNPJ.

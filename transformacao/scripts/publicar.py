"""Publica a camada ouro (DuckDB) no PostgreSQL de servir.

Carrega em um schema de staging e troca de schema numa transação, então a API
nunca enxerga uma carga pela metade.

Uso: uv run python scripts/publicar.py [--pg "postgresql://..."]
"""

import argparse
import os
import re
from pathlib import Path

import duckdb

DUCKDB = Path(__file__).resolve().parents[2] / "dados" / "checaai.duckdb"
PG_PADRAO = "postgresql://checaai:checaai_dev@localhost:5442/checaai"

DDL = """
CREATE EXTENSION IF NOT EXISTS pg_trgm;
DROP SCHEMA IF EXISTS ouro_carga CASCADE;
CREATE SCHEMA ouro_carga;
CREATE TABLE ouro_carga.entidade_texto (
    id text, tipo text, nome text, documento text, atributos text, fonte text, referencia text
);
CREATE TABLE ouro_carga.relacao_texto (
    origem_id text, destino_id text, tipo text, atributos text, fonte text, referencia text
);
"""

TROCA = """
CREATE TABLE ouro_carga.entidade AS
    SELECT id, tipo, nome, documento, atributos::jsonb AS atributos, fonte, referencia
    FROM ouro_carga.entidade_texto;
ALTER TABLE ouro_carga.entidade ADD PRIMARY KEY (id);
CREATE INDEX ON ouro_carga.entidade (tipo, documento);
CREATE INDEX ON ouro_carga.entidade USING gin (nome gin_trgm_ops);

CREATE TABLE ouro_carga.relacao AS
    SELECT origem_id, destino_id, tipo, atributos::jsonb AS atributos, fonte, referencia
    FROM ouro_carga.relacao_texto;
CREATE INDEX ON ouro_carga.relacao (origem_id);
CREATE INDEX ON ouro_carga.relacao (destino_id);

DROP TABLE ouro_carga.entidade_texto, ouro_carga.relacao_texto;

DO $$ BEGIN DROP SCHEMA IF EXISTS ouro CASCADE; ALTER SCHEMA ouro_carga RENAME TO ouro; END $$;
"""


def executar_no_postgres(con: duckdb.DuckDBPyConnection, sql: str) -> None:
    # postgres_execute usa prepared statements, que aceitam um comando por vez. Por isso
    # executamos comando a comando (separados por ";" + quebra de linha); a troca final é um
    # bloco DO numa linha só (um comando, atômico).
    # O SQL é constante deste arquivo (sem entrada externa), então pode ir literal.
    for comando in filter(str.strip, re.split(r";\s*\n", sql)):
        con.execute(f"CALL postgres_execute('pg', '{comando.replace(chr(39), chr(39) * 2)}')")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pg", default=os.environ.get("CHECAAI_PG", PG_PADRAO))
    args = parser.parse_args()

    # Conexão em memória com dois bancos anexados: o DuckDB local só para leitura e o
    # Postgres para escrita (abrir o arquivo local como read_only deixaria tudo read_only).
    con = duckdb.connect()
    con.execute("INSTALL postgres; LOAD postgres;")
    con.execute(f"ATTACH '{DUCKDB.as_posix()}' AS local (READ_ONLY)")
    con.execute(f"ATTACH '{args.pg}' AS pg (TYPE postgres)")

    executar_no_postgres(con, DDL)
    con.execute("INSERT INTO pg.ouro_carga.entidade_texto SELECT id, tipo, nome, documento, atributos::varchar, fonte, referencia FROM local.ouro.entidade")
    con.execute("INSERT INTO pg.ouro_carga.relacao_texto SELECT origem_id, destino_id, tipo, atributos::varchar, fonte, referencia FROM local.ouro.relacao")
    executar_no_postgres(con, TROCA)

    entidades, relacoes = con.execute(
        "SELECT (SELECT count(*) FROM local.ouro.entidade), (SELECT count(*) FROM local.ouro.relacao)"
    ).fetchone()
    print(f"Publicado: {entidades:,} entidades e {relacoes:,} relações.")


if __name__ == "__main__":
    main()

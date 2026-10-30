"""Convenções da camada bronze: dado bruto, imutável, particionado por fonte e referência.

Layout em disco (fora do git):
    dados/brutos/<fonte>/<referencia>/...          arquivos originais baixados (cache)
    dados/bronze/<fonte>/referencia=<ref>/<tabela>/*.parquet
"""

from pathlib import Path

RAIZ_DADOS = Path(__file__).resolve().parents[3] / "dados"

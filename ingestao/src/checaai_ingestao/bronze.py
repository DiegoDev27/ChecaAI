"""Convenções da camada bronze: dado bruto, imutável, particionado por fonte e data de coleta."""

from datetime import date
from pathlib import Path

RAIZ_DADOS = Path(__file__).resolve().parents[3] / "dados"


def pasta_bronze(fonte_id: str, coleta: date | None = None) -> Path:
    """Ex.: dados/bronze/receita-cnpj/coleta=2026-10-29/"""
    coleta = coleta or date.today()
    pasta = RAIZ_DADOS / "bronze" / fonte_id / f"coleta={coleta.isoformat()}"
    pasta.mkdir(parents=True, exist_ok=True)
    return pasta

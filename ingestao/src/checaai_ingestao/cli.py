"""Ponto de entrada: `checaai-coletar <fonte_id>` roda o coletor de uma fonte do catálogo."""

import argparse
import importlib
import sys
from pathlib import Path

import yaml

CATALOGO = Path(__file__).resolve().parents[3] / "catalogo" / "fontes.yaml"


def main() -> int:
    # O console do Windows usa cp1252 por padrão e quebra com acentos e setas.
    sys.stdout.reconfigure(encoding="utf-8")
    fontes = {f["id"]: f for f in yaml.safe_load(CATALOGO.read_text(encoding="utf-8"))["fontes"]}

    parser = argparse.ArgumentParser(prog="checaai-coletar")
    parser.add_argument("fonte", choices=sorted(fontes))
    parser.add_argument("--mes", help="referência AAAA-MM (padrão: a mais recente)")
    parser.add_argument("--fatias", help="fatias dos arquivos grandes, ex.: 1 ou 0-9")
    args = parser.parse_args()

    opcoes = {}
    if args.mes:
        opcoes["mes"] = args.mes
    if args.fatias:
        inicio, _, fim = args.fatias.partition("-")
        opcoes["fatias"] = [str(n) for n in range(int(inicio), int(fim or inicio) + 1)]

    # Cada fonte vira um módulo em coletores/ com uma função coletar(fonte: dict).
    modulo = f"checaai_ingestao.coletores.{args.fonte.replace('-', '_')}"
    try:
        coletor = importlib.import_module(modulo)
    except ModuleNotFoundError:
        print(f"Fonte '{args.fonte}' está no catálogo, mas ainda não tem coletor ({modulo}).", file=sys.stderr)
        return 1

    coletor.coletar(fontes[args.fonte], **opcoes)
    return 0


if __name__ == "__main__":
    sys.exit(main())

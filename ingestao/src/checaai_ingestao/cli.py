"""Ponto de entrada: `checaai-coletar <fonte_id>` roda o coletor de uma fonte do catálogo."""

import argparse
import importlib
import sys
from pathlib import Path

import yaml

CATALOGO = Path(__file__).resolve().parents[3] / "catalogo" / "fontes.yaml"


def main() -> int:
    fontes = {f["id"]: f for f in yaml.safe_load(CATALOGO.read_text(encoding="utf-8"))["fontes"]}

    parser = argparse.ArgumentParser(prog="checaai-coletar")
    parser.add_argument("fonte", choices=sorted(fontes))
    args = parser.parse_args()

    # Cada fonte vira um módulo em coletores/ com uma função coletar(fonte: dict).
    modulo = f"checaai_ingestao.coletores.{args.fonte.replace('-', '_')}"
    try:
        coletor = importlib.import_module(modulo)
    except ModuleNotFoundError:
        print(f"Fonte '{args.fonte}' está no catálogo, mas ainda não tem coletor ({modulo}).", file=sys.stderr)
        return 1

    coletor.coletar(fontes[args.fonte])
    return 0


if __name__ == "__main__":
    sys.exit(main())

"""Coletor do Cadastro Nacional da Pessoa Jurídica (Receita Federal).

A Receita publica um compartilhamento público no Nextcloud com uma pasta por mês (AAAA-MM),
cada uma com ZIPs de CSV (latin-1, separador ";", sem cabeçalho). Acessamos via WebDAV público.

Cada ZIP vira um Parquet bruto na camada bronze, com os nomes de coluna do layout oficial
e todos os campos como texto (tipar é trabalho da camada prata).
"""

import io
import re
import zipfile
from pathlib import Path
from xml.etree import ElementTree

import duckdb
import httpx

from checaai_ingestao.bronze import RAIZ_DADOS

WEBDAV = "https://arquivos.receitafederal.gov.br/public.php/webdav"
TOKEN_COMPARTILHAMENTO = "YggdBLfdninEJX9"

# Layout oficial: https://www.gov.br/receitafederal/dados/cnpj-metadados.pdf
COLUNAS = {
    "empresas": [
        "cnpj_basico", "razao_social", "natureza_juridica", "qualificacao_responsavel",
        "capital_social", "porte_empresa", "ente_federativo_responsavel",
    ],
    "estabelecimentos": [
        "cnpj_basico", "cnpj_ordem", "cnpj_dv", "identificador_matriz_filial", "nome_fantasia",
        "situacao_cadastral", "data_situacao_cadastral", "motivo_situacao_cadastral",
        "nome_cidade_exterior", "pais", "data_inicio_atividade", "cnae_fiscal_principal",
        "cnae_fiscal_secundaria", "tipo_logradouro", "logradouro", "numero", "complemento",
        "bairro", "cep", "uf", "municipio", "ddd_1", "telefone_1", "ddd_2", "telefone_2",
        "ddd_fax", "fax", "correio_eletronico", "situacao_especial", "data_situacao_especial",
    ],
    "socios": [
        "cnpj_basico", "identificador_socio", "nome_socio_razao_social", "cpf_cnpj_socio",
        "qualificacao_socio", "data_entrada_sociedade", "pais", "representante_legal",
        "nome_representante", "qualificacao_representante_legal", "faixa_etaria",
    ],
    # Tabelas auxiliares de domínio: código → descrição.
    "cnaes": ["codigo", "descricao"],
    "motivos": ["codigo", "descricao"],
    "municipios": ["codigo", "descricao"],
    "naturezas": ["codigo", "descricao"],
    "paises": ["codigo", "descricao"],
    "qualificacoes": ["codigo", "descricao"],
}

# Walking skeleton: só a fatia "1" das tabelas grandes (~450 MB). Use --fatias 0-9 para a base inteira.
FATIAS_PADRAO = ["1"]
TABELAS_FATIADAS = ["Empresas", "Estabelecimentos", "Socios"]
TABELAS_AUXILIARES = ["Cnaes", "Motivos", "Municipios", "Naturezas", "Paises", "Qualificacoes"]


def _cliente() -> httpx.Client:
    return httpx.Client(auth=(TOKEN_COMPARTILHAMENTO, ""), timeout=httpx.Timeout(60, read=300), follow_redirects=True)


def mes_mais_recente(cliente: httpx.Client) -> str:
    resposta = cliente.request("PROPFIND", f"{WEBDAV}/", headers={"Depth": "1"})
    resposta.raise_for_status()
    hrefs = [e.text or "" for e in ElementTree.fromstring(resposta.content).iter("{DAV:}href")]
    meses = sorted(m.group(1) for h in hrefs if (m := re.search(r"/(\d{4}-\d{2})/$", h)))
    if not meses:
        raise RuntimeError("Nenhuma pasta AAAA-MM encontrada no compartilhamento da Receita.")
    return meses[-1]


def baixar(cliente: httpx.Client, url: str, destino: Path) -> None:
    """Download com retomada: o servidor da Receita costuma derrubar conexões longas."""
    destino.parent.mkdir(parents=True, exist_ok=True)
    parcial = destino.with_suffix(destino.suffix + ".parcial")

    for tentativa in range(1, 6):
        ja_baixado = parcial.stat().st_size if parcial.exists() else 0
        cabecalhos = {"Range": f"bytes={ja_baixado}-"} if ja_baixado else {}
        try:
            with cliente.stream("GET", url, headers=cabecalhos) as resposta:
                if resposta.status_code == 416:  # já está completo
                    break
                resposta.raise_for_status()
                modo = "ab" if resposta.status_code == 206 else "wb"
                with parcial.open(modo) as arquivo:
                    for bloco in resposta.iter_bytes(1 << 20):
                        arquivo.write(bloco)
            break
        except (httpx.TransportError, httpx.HTTPStatusError) as erro:
            print(f"  tentativa {tentativa} falhou ({erro}); retomando…")
    else:
        raise RuntimeError(f"Falha ao baixar {url}")

    parcial.replace(destino)


def zip_para_parquet(zip_path: Path, tabela: str, parquet_path: Path) -> int:
    colunas = COLUNAS[tabela]
    pasta_tmp = zip_path.parent / f"{zip_path.stem}_csv"
    pasta_tmp.mkdir(exist_ok=True)
    csv_path = pasta_tmp / f"{zip_path.stem}.csv"

    # O leitor latin-1 do DuckDB é estrito e recusa alguns bytes que a Receita usa.
    # O Python decodifica latin-1 sem perda (cada byte vira um caractere); regravamos em UTF-8.
    with zipfile.ZipFile(zip_path) as z:
        (membro,) = z.namelist()
        with z.open(membro) as origem, csv_path.open("w", encoding="utf-8", newline="") as destino:
            texto = io.TextIOWrapper(origem, encoding="latin-1", newline="")
            while bloco := texto.read(1 << 22):
                destino.write(bloco)

    parquet_path.parent.mkdir(parents=True, exist_ok=True)
    nomes = ", ".join(f"'{c}': 'VARCHAR'" for c in colunas)
    con = duckdb.connect()
    con.execute(
        f"""
        COPY (
            SELECT * FROM read_csv(?, delim=';', quote='"', header=false,
                                   columns={{{nomes}}}, strict_mode=false)
        ) TO '{parquet_path.as_posix()}' (FORMAT parquet, COMPRESSION zstd)
        """,
        [str(csv_path)],
    )
    linhas = con.execute(f"SELECT count(*) FROM '{parquet_path.as_posix()}'").fetchone()[0]

    csv_path.unlink()
    pasta_tmp.rmdir()
    return linhas


def coletar(fonte: dict, fatias: list[str] | None = None, mes: str | None = None) -> None:
    fatias = fatias or FATIAS_PADRAO
    with _cliente() as cliente:
        mes = mes or mes_mais_recente(cliente)
        print(f"Receita CNPJ: referência {mes}, fatias {fatias}")

        arquivos = [f"{t}{n}" for t in TABELAS_FATIADAS for n in fatias] + TABELAS_AUXILIARES
        pasta_zip = RAIZ_DADOS / "brutos" / fonte["id"] / mes
        pasta_bronze = RAIZ_DADOS / "bronze" / fonte["id"] / f"referencia={mes}"

        for nome in arquivos:
            tabela = re.sub(r"\d+$", "", nome).lower()
            parquet = pasta_bronze / tabela / f"{nome.lower()}.parquet"
            if parquet.exists():
                print(f"  {nome}: já está no bronze, pulando")
                continue

            zip_path = pasta_zip / f"{nome}.zip"
            if not zip_path.exists():
                print(f"  {nome}: baixando…")
                baixar(cliente, f"{WEBDAV}/{mes}/{nome}.zip", zip_path)

            linhas = zip_para_parquet(zip_path, tabela, parquet)
            print(f"  {nome}: {linhas:,} linhas → {parquet.relative_to(RAIZ_DADOS)}")

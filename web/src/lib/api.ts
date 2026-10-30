const API_URL = process.env.API_URL ?? "http://localhost:5179";

export type Vinculo = {
  id: string;
  tipo: string;
  nome: string | null;
  documento: string | null;
  atributos: { qualificacao?: string; data_entrada?: string; faixa_etaria?: string; parcial?: boolean };
};

export type Empresa = {
  id: string;
  nome: string | null;
  cnpj: string | null;
  atributos: {
    parcial?: boolean;
    nome_fantasia?: string | null;
    situacao_cadastral?: string | null;
    data_inicio_atividade?: string | null;
    cnae_principal?: { codigo: string | null; descricao: string | null };
    natureza_juridica?: { codigo: string | null; descricao: string | null };
    porte?: string | null;
    capital_social?: number | null;
    endereco?: { municipio: string | null; uf: string | null; bairro: string | null };
    qtd_estabelecimentos?: number | null;
  };
  socios: Vinculo[];
  socioDe: Vinculo[];
  fonte: { id: string; nome: string; referencia: string; url: string };
};

export async function obterEmpresa(cnpj: string): Promise<Empresa | null> {
  const resposta = await fetch(`${API_URL}/v1/empresas/${encodeURIComponent(cnpj)}`, { next: { revalidate: 3600 } });
  if (resposta.status === 404 || resposta.status === 400) return null;
  if (!resposta.ok) throw new Error(`API respondeu ${resposta.status}`);
  return resposta.json();
}

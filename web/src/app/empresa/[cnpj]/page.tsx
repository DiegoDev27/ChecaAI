import Link from "next/link";
import { notFound } from "next/navigation";
import { obterEmpresa, type Vinculo } from "@/lib/api";

function formatarCnpj(cnpj: string | null) {
  if (!cnpj) return "";
  return cnpj.length === 14 ? cnpj.replace(/^(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})$/, "$1.$2.$3/$4-$5") : cnpj;
}

function formatarData(iso?: string | null) {
  return iso ? new Date(`${iso}T00:00:00`).toLocaleDateString("pt-BR") : "—";
}

function Campo({ rotulo, valor }: { rotulo: string; valor?: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-borda bg-superficie p-4">
      <dt className="text-xs uppercase tracking-wider text-texto-suave">{rotulo}</dt>
      <dd className="mt-1 font-semibold">{valor || "—"}</dd>
    </div>
  );
}

function ListaVinculos({ titulo, itens, vazio }: { titulo: string; itens: Vinculo[]; vazio: string }) {
  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-xl font-bold">
        {titulo} <span className="text-texto-suave">({itens.length})</span>
      </h2>
      {itens.length === 0 ? (
        <p className="text-texto-suave">{vazio}</p>
      ) : (
        <ul className="divide-y divide-borda rounded-lg border border-borda bg-superficie">
          {itens.map((v) => (
            <li key={v.id} className="flex flex-col gap-1 p-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                {v.tipo === "empresa" ? (
                  <Link href={`/empresa/${v.id.split(":")[1]}`} className="font-semibold text-destaque hover:underline">
                    {v.nome ?? formatarCnpj(v.documento)}
                  </Link>
                ) : (
                  <span className="font-semibold">{v.nome}</span>
                )}
                <p className="text-sm text-texto-suave">
                  {[v.atributos.qualificacao, v.tipo === "pessoa" ? v.documento : null].filter(Boolean).join(" · ")}
                </p>
              </div>
              <span className="text-sm text-texto-suave">desde {formatarData(v.atributos.data_entrada)}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export async function generateMetadata({ params }: { params: Promise<{ cnpj: string }> }) {
  const empresa = await obterEmpresa((await params).cnpj);
  return { title: empresa ? `${empresa.nome ?? empresa.cnpj} — ChecaAI` : "Empresa não encontrada — ChecaAI" };
}

export default async function PaginaEmpresa({ params }: { params: Promise<{ cnpj: string }> }) {
  const empresa = await obterEmpresa((await params).cnpj);
  if (!empresa) notFound();

  const a = empresa.atributos;
  const ativa = a.situacao_cadastral === "Ativa";

  return (
    <main className="mx-auto flex max-w-4xl flex-col gap-10 px-4 py-10">
      <Link href="/" className="text-sm text-texto-suave hover:text-destaque">
        ← Checa<span className="text-destaque">AI</span>
      </Link>

      <header className="flex flex-col gap-3">
        <span className="text-sm uppercase tracking-widest text-texto-suave">Empresa · CNPJ {formatarCnpj(empresa.cnpj)}</span>
        <h1 className="text-3xl font-extrabold sm:text-4xl">{empresa.nome ?? "Nome não disponível"}</h1>
        {a.nome_fantasia && <p className="text-lg text-texto-suave">{a.nome_fantasia}</p>}
        {a.situacao_cadastral && (
          <span
            className={`w-fit rounded-full px-3 py-1 text-sm font-bold ${ativa ? "bg-destaque text-sobre-destaque" : "border border-destaque text-destaque"}`}
          >
            {a.situacao_cadastral}
          </span>
        )}
        {a.parcial && (
          <p className="rounded-lg border border-borda p-3 text-sm text-texto-suave">
            Só temos dados parciais desta empresa: ela aparece num quadro de sócios, mas o cadastro dela ainda não foi carregado.
          </p>
        )}
      </header>

      {!a.parcial && (
        <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <Campo rotulo="Abertura" valor={formatarData(a.data_inicio_atividade)} />
          <Campo rotulo="Atividade principal" valor={a.cnae_principal?.descricao} />
          <Campo rotulo="Natureza jurídica" valor={a.natureza_juridica?.descricao} />
          <Campo rotulo="Porte" valor={a.porte} />
          <Campo
            rotulo="Capital social"
            valor={a.capital_social != null ? a.capital_social.toLocaleString("pt-BR", { style: "currency", currency: "BRL" }) : null}
          />
          <Campo rotulo="Sede" valor={[a.endereco?.municipio, a.endereco?.uf].filter(Boolean).join(" / ")} />
        </dl>
      )}

      <ListaVinculos titulo="Sócios e administradores" itens={empresa.socios} vazio="Nenhum sócio nesta carga." />
      <ListaVinculos titulo="É sócia de" itens={empresa.socioDe} vazio="Não aparece como sócia de outras empresas nesta carga." />

      <footer className="border-t border-borda pt-6 text-sm text-texto-suave">
        Fonte:{" "}
        <a href={empresa.fonte.url} className="text-destaque hover:underline" target="_blank" rel="noreferrer">
          {empresa.fonte.nome}
        </a>
        , referência {empresa.fonte.referencia}. O CPF de sócios vem mascarado pela própria Receita.
      </footer>
    </main>
  );
}

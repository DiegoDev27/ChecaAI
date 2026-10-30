export default function Home() {
  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col justify-center gap-8 px-4 py-16">
      <span className="w-fit rounded-full border border-destaque px-3 py-1 text-xs font-semibold uppercase tracking-widest text-destaque">
        Em reconstrução
      </span>
      <h1 className="text-4xl font-extrabold leading-tight sm:text-6xl">
        Checa<span className="text-destaque">AI</span>
      </h1>
      <p className="text-lg text-texto-suave">
        Todos os dados públicos do Estado brasileiro num só lugar, ligados entre si: quem é quem, quem paga quem e de
        onde veio cada número. Sem opinião, só fatos e a fonte oficial.
      </p>
      <form action="/busca" className="flex flex-col gap-3 sm:flex-row">
        <input
          name="q"
          placeholder="Digite um CNPJ (busca por nome em breve)"
          className="flex-1 rounded-lg border border-borda bg-superficie px-4 py-3 text-texto placeholder:text-texto-suave focus:border-destaque focus:outline-none"
        />
        <button
          type="submit"
          className="rounded-lg bg-destaque px-6 py-3 font-bold text-sobre-destaque hover:bg-destaque-forte"
        >
          Checar
        </button>
      </form>
    </main>
  );
}

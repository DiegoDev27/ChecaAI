import { redirect } from "next/navigation";

// Por enquanto a busca só entende CNPJ; a busca por nome vem com o índice pg_trgm.
export default async function Busca({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const digitos = ((await searchParams).q ?? "").replace(/\D/g, "");
  if (digitos.length === 14 || digitos.length === 8) redirect(`/empresa/${digitos}`);

  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col justify-center gap-4 px-4">
      <h1 className="text-2xl font-bold">Por enquanto, a busca só aceita CNPJ.</h1>
      <p className="text-texto-suave">Digite os 14 dígitos (com ou sem pontuação). A busca por nome vem em breve.</p>
    </main>
  );
}

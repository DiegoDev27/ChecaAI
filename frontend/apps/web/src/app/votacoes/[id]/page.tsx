import type { Metadata } from 'next';
import { SessionDetail } from '@/components/sessions/SessionDetail';

type Props = { params: { id: string } };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return {
    title: `Votação #${params.id}`,
    description: 'Detalhes da sessão de votação com breakdown por partido e análise IA.',
  };
}

export default function VotacaoDetailPage({ params }: Props) {
  const id = parseInt(params.id, 10);
  if (isNaN(id)) {
    return <div className="container mx-auto px-4 py-16 text-center text-gray-500">ID inválido.</div>;
  }

  return (
    <div className="container mx-auto px-4 py-8 max-w-5xl">
      <SessionDetail id={id} />
    </div>
  );
}

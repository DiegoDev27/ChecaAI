import type { Metadata } from 'next';
import { PoliticianProfile } from '@/components/politicians/PoliticianProfile';

type Props = { params: { id: string } };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return {
    title: `Parlamentar #${params.id}`,
    description: `Perfil completo, votos, despesas e comissões do parlamentar.`,
  };
}

export default function ParlamentarDetailPage({ params }: Props) {
  const id = parseInt(params.id, 10);

  if (isNaN(id)) {
    return (
      <div className="container mx-auto px-4 py-16 text-center text-slate-500">
        ID inválido.
      </div>
    );
  }

  return (
    <div className="container mx-auto px-4 py-8 max-w-5xl">
      <PoliticianProfile id={id} />
    </div>
  );
}

import type { Metadata } from 'next';
import { ProposalDetail } from '@/components/proposals/ProposalDetail';

type Props = { params: { id: string } };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return {
    title: `Proposição #${params.id}`,
    description: 'Detalhes da proposição legislativa com ementa completa e votações relacionadas.',
  };
}

export default function ProposalDetailPage({ params }: Props) {
  const id = parseInt(params.id, 10);
  if (isNaN(id)) {
    return (
      <div className="container mx-auto px-4 py-16 text-center text-gray-500">
        ID inválido.
      </div>
    );
  }

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      <ProposalDetail id={id} />
    </div>
  );
}

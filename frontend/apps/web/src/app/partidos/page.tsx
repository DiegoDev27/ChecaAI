import type { Metadata } from 'next';
import { PartiesList } from '@/components/common/PartiesList';
import { Flag } from 'lucide-react';

export const metadata: Metadata = {
  title: 'Partidos Políticos | Checa AI',
  description:
    'Todos os partidos políticos brasileiros: sigla, número eleitoral, presidente e total de membros.',
};

export default function PartidosPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-5xl">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <div className="bg-brand-100 rounded-xl p-2">
            <Flag className="h-6 w-6 text-brand-700" />
          </div>
          <div>
            <h1 className="text-3xl font-bold text-gray-900">Partidos Políticos</h1>
            <p className="text-gray-600 text-sm mt-0.5">
              Sigla, número eleitoral, presidente e membros de cada partido
            </p>
          </div>
        </div>
      </div>
      <PartiesList />
    </div>
  );
}

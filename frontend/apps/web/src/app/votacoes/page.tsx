import type { Metadata } from 'next';
import { SessionList } from '@/components/sessions/SessionList';

export const metadata: Metadata = {
  title: 'Votações',
  description: 'Acompanhe votações da Câmara dos Deputados e do Senado Federal.',
};

export default function VotacoesPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-5xl">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-2">Votações</h1>
        <p className="text-gray-600">
          Sessões de votação nominal e simbólica da Câmara dos Deputados e do Senado Federal.
        </p>
      </div>
      <SessionList />
    </div>
  );
}

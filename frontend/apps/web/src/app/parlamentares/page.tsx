import type { Metadata } from 'next';
import { PoliticianSearch } from '@/components/politicians/PoliticianSearch';

export const metadata: Metadata = {
  title: 'Parlamentares',
  description: 'Busque e filtre parlamentares brasileiros por nome, partido, estado e cargo.',
};

export default function ParlamentaresPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-6xl">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-slate-900 mb-2">Parlamentares</h1>
        <p className="text-slate-600">
          Pesquise senadores, deputados, governadores, prefeitos e vereadores de todo o Brasil.
        </p>
      </div>
      <PoliticianSearch />
    </div>
  );
}

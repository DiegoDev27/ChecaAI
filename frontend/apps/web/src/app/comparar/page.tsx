import type { Metadata } from 'next';
import { PoliticianCompare } from '@/components/compare/PoliticianCompare';
import { ArrowLeftRight } from 'lucide-react';

export const metadata: Metadata = {
  title: 'Comparar Parlamentares',
  description:
    'Compare dois parlamentares lado a lado: votos em comum, diferenças, despesas e análise com IA.',
};

export default function CompararPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-5xl">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <div className="bg-primary-100 rounded-xl p-2">
            <ArrowLeftRight className="h-6 w-6 text-primary-700" />
          </div>
          <div>
            <h1 className="text-3xl font-bold text-slate-900">Comparar Parlamentares</h1>
            <p className="text-slate-600 text-sm mt-0.5">
              Votos em comum, diferenças, despesas e análise IA lado a lado
            </p>
          </div>
        </div>
      </div>
      <PoliticianCompare />
    </div>
  );
}

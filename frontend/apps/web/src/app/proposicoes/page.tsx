import type { Metadata } from 'next';
import { ProposalList } from '@/components/proposals/ProposalList';
import { FileText } from 'lucide-react';

export const metadata: Metadata = {
  title: 'Proposições',
  description:
    'Projetos de lei, PECs, MPVs e outras proposições legislativas da Câmara e do Senado.',
};

export default function ProposicoesPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <div className="bg-primary-100 rounded-xl p-2">
            <FileText className="h-6 w-6 text-primary-700" />
          </div>
          <div>
            <h1 className="text-3xl font-bold text-slate-900">Proposições Legislativas</h1>
            <p className="text-slate-600 text-sm mt-0.5">
              PLs, PECs, MPVs e demais proposições da Câmara dos Deputados e Senado Federal
            </p>
          </div>
        </div>
      </div>
      <ProposalList />
    </div>
  );
}

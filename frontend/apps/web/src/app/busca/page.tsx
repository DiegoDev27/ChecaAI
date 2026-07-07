import type { Metadata } from 'next';
import { AiChat } from '@/components/ai/AiChat';
import { Sparkles, ShieldCheck } from 'lucide-react';

export const metadata: Metadata = {
  title: 'Busca Inteligente com IA',
  description:
    'Pergunte sobre parlamentares, votações, gastos e propostas em linguagem natural. Powered by Claude AI.',
};

export default function BuscaPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-3xl">
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center gap-3 mb-3">
          <div className="bg-primary-100 rounded-xl p-2.5">
            <Sparkles className="h-6 w-6 text-primary-700" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-slate-900">Busca Inteligente com IA</h1>
            <p className="text-slate-500 text-sm">
              Pergunte sobre política brasileira em linguagem natural
            </p>
          </div>
        </div>

        {/* Info chips */}
        <div className="flex flex-wrap gap-2 mt-3">
          <span className="flex items-center gap-1.5 text-xs bg-primary-50 text-primary-700 border border-primary-200 px-3 py-1 rounded-full">
            <Sparkles className="h-3 w-3" />
            Powered by Claude AI (Anthropic)
          </span>
          <span className="flex items-center gap-1.5 text-xs bg-slate-100 text-slate-600 border border-slate-200 px-3 py-1 rounded-full">
            <ShieldCheck className="h-3 w-3" />
            Dados reais do Congresso Nacional
          </span>
        </div>
      </div>

      {/* Chat */}
      <AiChat />

      {/* Disclaimer */}
      <p className="text-center text-xs text-slate-400 mt-4">
        As respostas são geradas por IA e podem conter imprecisões. Sempre verifique em fontes
        oficiais. Os dados de votações, gastos e salários são obtidos diretamente das APIs do
        governo federal.
      </p>
    </div>
  );
}

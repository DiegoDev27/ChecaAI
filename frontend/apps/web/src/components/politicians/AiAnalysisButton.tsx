'use client';

import { useState } from 'react';
import { Sparkles, X, Loader2 } from 'lucide-react';
import { useCompletion } from 'ai/react';

interface Props {
  politicianId: number;
  politicianName: string;
}

export function AiAnalysisButton({ politicianId, politicianName }: Props) {
  const [open, setOpen] = useState(false);

  const { completion, complete, isLoading, error } = useCompletion({
    api: `/api/ai/politician/${politicianId}/analysis`,
    streamProtocol: 'data',
  });

  const handleOpen = () => {
    setOpen(true);
    if (!completion) {
      complete('');
    }
  };

  return (
    <>
      <button
        onClick={handleOpen}
        className="flex items-center gap-2 text-sm bg-primary-50 text-primary-700 border border-primary-200 px-4 py-2 rounded-lg hover:bg-primary-100 transition-colors font-medium"
      >
        <Sparkles className="h-4 w-4" />
        Análise IA
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[80vh] flex flex-col animate-fade-scale-in">
            {/* Header */}
            <div className="flex items-center justify-between p-4 border-b">
              <div className="flex items-center gap-2 font-semibold text-slate-900">
                <Sparkles className="h-5 w-5 text-primary-600" />
                Análise IA — {politicianName}
              </div>
              <button
                onClick={() => setOpen(false)}
                className="p-1 rounded hover:bg-slate-100 text-slate-500"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            {/* Content */}
            <div className="flex-1 overflow-y-auto p-5">
              {isLoading && !completion && (
                <div className="flex items-center gap-3 text-slate-500">
                  <Loader2 className="h-5 w-5 animate-spin" />
                  Gerando análise...
                </div>
              )}
              {error && (
                <div className="text-red-600 text-sm">
                  Erro ao gerar análise. Verifique se a API Key do Claude está configurada.
                </div>
              )}
              {completion && (
                <div className="prose prose-sm max-w-none text-slate-800 whitespace-pre-wrap leading-relaxed">
                  {completion}
                  {isLoading && <span className="inline-block w-1 h-4 bg-primary-600 animate-pulse ml-0.5" />}
                </div>
              )}
            </div>

            <div className="p-3 border-t text-xs text-slate-400 text-center">
              Análise gerada por Claude AI com dados reais do ChecaAI
            </div>
          </div>
        </div>
      )}
    </>
  );
}

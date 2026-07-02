'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { transparencyApi } from '@checaai/api-client';
import { formatBRL } from '@/lib/utils';
import { Loader2, ChevronLeft, ChevronRight } from 'lucide-react';

interface Props { id: number }

const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i);

const ALLOWANCE_LABELS: Record<string, string> = {
  Housing: 'Auxílio moradia',
  Health: 'Auxílio saúde',
  Education: 'Auxílio educação',
  Transport: 'Auxílio transporte',
  Food: 'Auxílio alimentação',
  PreSchool: 'Auxílio pré-escolar',
  Clothing: 'Auxílio fardamento',
  Pension: 'Pensão especial',
  Other: 'Outros auxílios',
};

export function AllowancesTab({ id }: Props) {
  const [page, setPage] = useState(1);
  const [year, setYear] = useState<number>(CURRENT_YEAR);
  const pageSize = 12;

  const { data, isLoading, isError } = useQuery({
    queryKey: ['politician-allowances', id, year, page],
    queryFn: () => transparencyApi.allowances(id, year, page, pageSize),
    placeholderData: (prev) => prev,
  });

  if (isLoading) {
    return <div className="flex justify-center py-12"><Loader2 className="h-8 w-8 animate-spin text-brand-600" /></div>;
  }

  if (isError) {
    return <div className="text-center py-12 text-red-600 text-sm">Erro ao carregar auxílios.</div>;
  }

  if (!data || data.totalCount === 0) {
    return (
      <div className="bg-white rounded-xl border p-8 text-center">
        <div className="text-gray-400 text-sm">
          Nenhum dado de auxílios disponível para {year}. Os dados são obtidos do Portal da Transparência (CGU).
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border overflow-hidden">
      <div className="p-4 border-b flex items-center gap-3 flex-wrap bg-gray-50">
        <select
          value={year}
          onChange={(e) => { setYear(Number(e.target.value)); setPage(1); }}
          className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
        >
          {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
        </select>
        <span className="text-sm text-gray-500 ml-auto">
          {data.totalCount} registros
        </span>
      </div>

      <div className="divide-y">
        {data.data.map((summary, i) => (
          <div key={`${summary.year}-${summary.month}-${i}`} className="px-5 py-4">
            <div className="flex items-center justify-between mb-3">
              <div className="font-medium text-gray-800">
                {String(summary.month).padStart(2, '0')}/{summary.year}
              </div>
              <div className="text-sm font-bold text-gray-900">
                Total: {formatBRL(summary.total)}
              </div>
            </div>
            {summary.items && summary.items.length > 0 ? (
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                {summary.items.map((item) => (
                  <div key={item.id} className="bg-gray-50 rounded-lg p-2.5 text-sm">
                    <div className="text-xs text-gray-500 mb-0.5">
                      {ALLOWANCE_LABELS[item.allowanceType] ?? item.allowanceType}
                    </div>
                    <div className="font-semibold text-gray-900">{formatBRL(item.amount)}</div>
                    {item.description && (
                      <div className="text-xs text-gray-400 mt-0.5 truncate">{item.description}</div>
                    )}
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-sm text-gray-400">Sem itens detalhados</div>
            )}
          </div>
        ))}
      </div>

      {data.totalPages > 1 && (
        <div className="flex items-center justify-between px-5 py-3 border-t bg-gray-50">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={!data.hasPrevPage}
            className="flex items-center gap-1 text-sm px-3 py-1.5 rounded-lg border disabled:opacity-40 disabled:cursor-not-allowed hover:bg-white transition-colors"
          >
            <ChevronLeft className="h-4 w-4" />
            Anterior
          </button>
          <span className="text-sm text-gray-500">Página {data.page} de {data.totalPages}</span>
          <button
            onClick={() => setPage((p) => p + 1)}
            disabled={!data.hasNextPage}
            className="flex items-center gap-1 text-sm px-3 py-1.5 rounded-lg border disabled:opacity-40 disabled:cursor-not-allowed hover:bg-white transition-colors"
          >
            Próxima
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      )}
    </div>
  );
}

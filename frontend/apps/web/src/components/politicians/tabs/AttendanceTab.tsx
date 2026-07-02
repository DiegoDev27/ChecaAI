'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { transparencyApi } from '@checaai/api-client';
import { formatDate, cn } from '@/lib/utils';
import { Loader2, ChevronLeft, ChevronRight, CheckCircle2, XCircle } from 'lucide-react';

interface Props { id: number }

const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i);

export function AttendanceTab({ id }: Props) {
  const [page, setPage] = useState(1);
  const [year, setYear] = useState<number>(CURRENT_YEAR);
  const pageSize = 25;

  const { data, isLoading, isError } = useQuery({
    queryKey: ['politician-attendance', id, year, page],
    queryFn: () => transparencyApi.attendance(id, year, page, pageSize),
    placeholderData: (prev) => prev,
  });

  if (isLoading) {
    return <div className="flex justify-center py-12"><Loader2 className="h-8 w-8 animate-spin text-brand-600" /></div>;
  }

  if (isError) {
    return <div className="text-center py-12 text-red-600 text-sm">Erro ao carregar dados de presença.</div>;
  }

  if (!data || data.totalCount === 0) {
    return (
      <div className="bg-white rounded-xl border p-8 text-center">
        <div className="text-gray-400 text-sm">
          Nenhum dado de presença disponível para {year}. Os dados são obtidos da API da Câmara dos Deputados (somente deputados federais).
        </div>
      </div>
    );
  }

  const overallPresent = data.presentCount ?? 0;
  const overallTotal = data.totalCount;
  const presenceRate = overallTotal > 0 ? ((overallPresent / overallTotal) * 100).toFixed(1) : '—';
  const rateNum = Number(presenceRate);

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
        <div className="ml-auto flex items-center gap-3">
          <span className={cn(
            'text-sm font-bold px-3 py-1 rounded-full',
            rateNum >= 80
              ? 'bg-green-100 text-green-800'
              : rateNum >= 60
              ? 'bg-yellow-100 text-yellow-800'
              : 'bg-red-100 text-red-800',
          )}>
            {presenceRate}% de presença em {year}
          </span>
          <span className="text-sm text-gray-500">
            {overallPresent} presente{overallPresent !== 1 ? 's' : ''} / {overallTotal} sessões
          </span>
        </div>
      </div>

      <div className="divide-y">
        {data.data.map((a, i) => (
          <div key={`${a.id}-${i}`} className="flex items-center gap-4 px-5 py-3 hover:bg-gray-50 transition-colors">
            {a.isPresent ? (
              <CheckCircle2 className="h-5 w-5 text-green-500 flex-shrink-0" />
            ) : (
              <XCircle className="h-5 w-5 text-red-400 flex-shrink-0" />
            )}
            <div className="flex-1 min-w-0">
              <div className="text-sm text-gray-800">
                {formatDate(a.sessionDate)} — {a.chamber}
              </div>
              {!a.isPresent && (a.absenceReason || a.absenceJustification) && (
                <div className="text-xs text-orange-600 mt-0.5">
                  {a.absenceJustification ?? a.absenceReason}
                </div>
              )}
            </div>
            <span className={cn(
              'text-xs font-medium px-2 py-0.5 rounded-full flex-shrink-0',
              a.isPresent ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600',
            )}>
              {a.isPresent ? 'Presente' : 'Ausente'}
            </span>
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

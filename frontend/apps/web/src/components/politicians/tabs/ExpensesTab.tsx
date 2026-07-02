'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checaai/api-client';
import { formatBRL, formatDate, cn } from '@/lib/utils';
import { Loader2, ChevronLeft, ChevronRight } from 'lucide-react';
import type { ExpenseSummary } from '@checaai/types';

interface Props { id: number; expenseSummary?: ExpenseSummary | null }

const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i);

const CATEGORY_COLORS = [
  'bg-brand-500', 'bg-civic-500', 'bg-orange-400', 'bg-purple-500',
  'bg-pink-400', 'bg-teal-500', 'bg-yellow-500', 'bg-red-400',
];

function ExpenseChart({ summary }: { summary: ExpenseSummary }) {
  const sorted = [...summary.byCategory].sort((a, b) => b.total - a.total).slice(0, 8);
  const max = sorted[0]?.total ?? 1;

  return (
    <div className="bg-white rounded-xl border p-5 mb-4">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-700">Despesas por categoria</h3>
        <span className="text-xs text-gray-400">
          Total: {formatBRL(summary.total)} • {summary.count.toLocaleString('pt-BR')} lançamentos
        </span>
      </div>
      <div className="space-y-2.5">
        {sorted.map((cat, i) => (
          <div key={cat.category}>
            <div className="flex items-center justify-between text-xs mb-1">
              <span className="text-gray-600 truncate max-w-[60%]">{cat.category}</span>
              <span className="font-medium text-gray-800 flex-shrink-0 ml-2">
                {formatBRL(cat.total)}
                <span className="text-gray-400 ml-1">({((cat.total / summary.total) * 100).toFixed(1)}%)</span>
              </span>
            </div>
            <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
              <div
                className={cn('h-full rounded-full', CATEGORY_COLORS[i % CATEGORY_COLORS.length])}
                style={{ width: `${(cat.total / max) * 100}%` }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export function ExpensesTab({ id, expenseSummary }: Props) {
  const [page, setPage] = useState(1);
  const [year, setYear] = useState<number | undefined>(undefined);
  const pageSize = 25;

  const { data, isLoading, isError } = useQuery({
    queryKey: ['politician-expenses', id, year, page],
    queryFn: () => politiciansApi.expenses(id, year, undefined, page, pageSize),
    placeholderData: (prev) => prev,
  });

  const totalThisPage = data?.data.reduce((sum, e) => sum + e.amount, 0) ?? 0;

  return (
    <div className="space-y-0">
      {/* Category chart */}
      {expenseSummary && expenseSummary.count > 0 && (
        <ExpenseChart summary={expenseSummary} />
      )}

      <div className="bg-white rounded-xl border overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b flex items-center gap-3 flex-wrap bg-gray-50">
          <select
            value={year ?? ''}
            onChange={(e) => { setYear(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
            className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="">Todos os anos</option>
            {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
          <div className="ml-auto text-right">
            {data && (
              <>
                <div className="text-xs text-gray-500">{data.totalCount.toLocaleString('pt-BR')} lançamentos</div>
                {totalThisPage > 0 && data.page === 1 && data.totalPages === 1 && (
                  <div className="text-sm font-bold text-gray-900">{formatBRL(totalThisPage)} total</div>
                )}
              </>
            )}
          </div>
        </div>

        {isLoading ? (
          <div className="flex justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-brand-600" />
          </div>
        ) : isError ? (
          <div className="text-center py-12 text-red-600 text-sm">Erro ao carregar despesas.</div>
        ) : (
          <>
            <div className="divide-y">
              {(data?.data ?? []).map((e, i) => (
                <div key={`${e.id}-${i}`} className="px-5 py-3.5 hover:bg-gray-50 transition-colors">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="text-sm font-medium text-gray-800 truncate">{e.category}</div>
                      <div className="text-xs text-gray-500 mt-0.5 truncate">
                        {e.provider || 'Fornecedor não informado'}
                      </div>
                      <div className="text-xs text-gray-400 mt-0.5">
                        {e.expenseDate ? formatDate(e.expenseDate) : `${String(e.month).padStart(2,'0')}/${e.year}`}
                        {e.documentNumber && ` • Doc: ${e.documentNumber}`}
                      </div>
                    </div>
                    <div className="text-right flex-shrink-0">
                      <div className={cn(
                        'text-sm font-bold',
                        e.amount > 10000 ? 'text-red-700' : e.amount > 5000 ? 'text-orange-700' : 'text-gray-900',
                      )}>
                        {formatBRL(e.amount)}
                      </div>
                      {e.documentNumber && (
                        <div className="text-xs text-gray-400 mt-1">
                          Doc: {e.documentNumber}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ))}
              {(data?.data ?? []).length === 0 && (
                <div className="text-center py-12 px-6">
                  <svg className="h-10 w-10 mx-auto mb-3 text-gray-200" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 14l6-6m-5.5.5h.01m4.99 5h.01M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16l3.5-2 3.5 2 3.5-2 3.5 2z" />
                  </svg>
                  <p className="text-gray-500 font-medium text-sm mb-1">
                    {year ? `Nenhuma despesa em ${year}` : 'Nenhuma despesa encontrada'}
                  </p>
                  {year && (
                    <button
                      onClick={() => setYear(undefined)}
                      className="text-brand-600 text-sm hover:underline mt-1"
                    >
                      Ver todos os anos
                    </button>
                  )}
                  {!year && (
                    <p className="text-gray-400 text-sm">
                      As despesas de cota parlamentar (CEAP) estão sendo sincronizadas. Tente novamente em breve.
                    </p>
                  )}
                </div>
              )}
            </div>

            {data && data.totalPages > 1 && (
              <div className="flex items-center justify-between px-5 py-3 border-t bg-gray-50">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={!data.hasPrevPage}
                  className="flex items-center gap-1 text-sm px-3 py-1.5 rounded-lg border disabled:opacity-40 disabled:cursor-not-allowed hover:bg-white transition-colors"
                >
                  <ChevronLeft className="h-4 w-4" />
                  Anterior
                </button>
                <span className="text-sm text-gray-500">
                  Página {data.page} de {data.totalPages}
                </span>
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
          </>
        )}
      </div>
    </div>
  );
}

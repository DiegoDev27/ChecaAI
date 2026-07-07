'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { transparencyApi } from '@checaai/api-client';
import { formatBRL, cn } from '@/lib/utils';
import { Loader2 } from 'lucide-react';

interface Props { id: number }

const CURRENT_YEAR = new Date().getFullYear();
// Election years 2022, 2020, 2018, 2016
const ELECTION_YEARS = [2024, 2022, 2020, 2018];

export function CampaignTab({ id }: Props) {
  const [year, setYear] = useState<number>(2022);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['politician-campaign', id, year],
    queryFn: () => transparencyApi.campaignExpenses(id, year),
    staleTime: 30 * 60 * 1000,
  });

  if (isLoading) {
    return <div className="flex justify-center py-12"><Loader2 className="h-8 w-8 animate-spin text-primary-600" /></div>;
  }

  if (isError) {
    return <div className="text-center py-12 text-red-600 text-sm">Erro ao carregar dados de campanha.</div>;
  }

  const total = (data ?? []).reduce((sum, e) => sum + e.amount, 0);

  if (!data || data.length === 0) {
    return (
      <div className="bg-white rounded-xl border p-8 text-center space-y-3">
        <div className="flex gap-2 justify-center">
          {ELECTION_YEARS.map((y) => (
            <button
              key={y}
              onClick={() => setYear(y)}
              className={cn(
                'px-4 py-2 rounded-lg text-sm font-medium border transition-colors',
                year === y ? 'bg-primary-600 text-white border-primary-600' : 'hover:bg-slate-50',
              )}
            >
              {y}
            </button>
          ))}
        </div>
        <div className="text-slate-400 text-sm">
          Nenhum dado de campanha disponível para {year}. Os dados são obtidos do TSE.
        </div>
      </div>
    );
  }

  // Group by category
  const byCategory: Record<string, number> = {};
  for (const e of data) {
    byCategory[e.category] = (byCategory[e.category] ?? 0) + e.amount;
  }
  const categories = Object.entries(byCategory).sort((a, b) => b[1] - a[1]);

  return (
    <div className="space-y-4">
      {/* Year selector */}
      <div className="flex gap-2">
        {ELECTION_YEARS.map((y) => (
          <button
            key={y}
            onClick={() => setYear(y)}
            className={cn(
              'px-4 py-2 rounded-lg text-sm font-medium border transition-colors',
              year === y ? 'bg-primary-600 text-white border-primary-600' : 'hover:bg-slate-50 border-slate-200',
            )}
          >
            {y}
          </button>
        ))}
      </div>

      {/* Summary */}
      <div className="bg-white rounded-xl border p-5">
        <div className="flex items-baseline gap-3 mb-4">
          <span className="text-3xl font-bold text-slate-900">{formatBRL(total)}</span>
          <span className="text-sm text-slate-500">total gastos em {year} ({data.length} registros)</span>
        </div>
        <div className="space-y-2">
          {categories.slice(0, 8).map(([cat, val]) => (
            <div key={cat}>
              <div className="flex justify-between text-sm mb-1">
                <span className="text-slate-600 truncate mr-3">{cat}</span>
                <span className="font-medium flex-shrink-0">{formatBRL(val)}</span>
              </div>
              <div className="h-1.5 bg-slate-100 rounded-full">
                <div
                  className="h-full bg-primary-400 rounded-full"
                  style={{ width: `${(val / total) * 100}%` }}
                />
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Expense list */}
      <div className="bg-white rounded-xl border overflow-hidden">
        <div className="px-5 py-3 border-b bg-slate-50">
          <h3 className="font-semibold text-slate-700 text-sm">Lançamentos individuais</h3>
        </div>
        <div className="divide-y max-h-96 overflow-y-auto">
          {data.map((e, i) => (
            <div key={i} className="flex items-center justify-between gap-3 px-5 py-3 hover:bg-slate-50">
              <div className="flex-1 min-w-0">
                <div className="text-sm text-slate-800 truncate">{e.category}</div>
                <div className="text-xs text-slate-500 mt-0.5 truncate">
                  {e.supplier || 'Fornecedor não informado'}
                  {e.supplierCnpjCpf && ` • CNPJ: ${e.supplierCnpjCpf}`}
                </div>
                {e.description && (
                  <div className="text-xs text-slate-400 mt-0.5 truncate">{e.description}</div>
                )}
              </div>
              <div className={cn(
                'font-bold text-sm flex-shrink-0',
                e.amount > 50000 ? 'text-red-700' : 'text-slate-900',
              )}>
                {formatBRL(e.amount)}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

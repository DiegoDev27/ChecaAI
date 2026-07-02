'use client';

import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checaai/api-client';
import { formatBRL, cn } from '@/lib/utils';
import { Loader2, TrendingUp, TrendingDown, Minus } from 'lucide-react';

interface Props { id: number }

export function SalariesTab({ id }: Props) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['politician-salaries', id],
    queryFn: () => politiciansApi.salaries(id),
    staleTime: 10 * 60 * 1000,
  });

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-brand-600" />
      </div>
    );
  }

  if (isError) {
    return <div className="text-center py-12 text-red-600 text-sm">Erro ao carregar salários.</div>;
  }

  if (!data || data.length === 0) {
    return (
      <div className="bg-white rounded-xl border p-8 text-center">
        <div className="text-gray-400 text-sm">
          Nenhum dado salarial disponível. Os dados são obtidos do Portal da Transparência (CGU) e requerem o CPF do parlamentar.
        </div>
      </div>
    );
  }

  const sorted = [...data].sort((a, b) =>
    b.year !== a.year ? b.year - a.year : b.month - a.month,
  );

  return (
    <div className="bg-white rounded-xl border overflow-hidden">
      <div className="px-5 py-4 border-b bg-gray-50">
        <h3 className="font-semibold text-gray-800">Histórico salarial (CGU)</h3>
        <p className="text-xs text-gray-500 mt-0.5">
          Fonte: Portal da Transparência do Governo Federal
        </p>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-gray-400 border-b bg-gray-50">
              <th className="px-5 py-2.5 font-medium">Período</th>
              <th className="px-5 py-2.5 font-medium text-right">Bruto</th>
              <th className="px-5 py-2.5 font-medium text-right">Líquido</th>
              <th className="px-5 py-2.5 font-medium text-right">Outros</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {sorted.map((s, i) => {
              const prev = sorted[i + 1];
              const delta = prev ? s.grossSalary - prev.grossSalary : 0;
              const TrendIcon = delta > 0 ? TrendingUp : delta < 0 ? TrendingDown : Minus;
              const trendCls = delta > 0 ? 'text-green-600' : delta < 0 ? 'text-red-600' : 'text-gray-400';
              return (
                <tr key={`${s.year}-${s.month}`} className="hover:bg-gray-50 transition-colors">
                  <td className="px-5 py-3 font-medium text-gray-800">
                    {String(s.month).padStart(2, '0')}/{s.year}
                  </td>
                  <td className="px-5 py-3 text-right">
                    <div className="flex items-center justify-end gap-1.5">
                      <TrendIcon className={cn('h-3.5 w-3.5', trendCls)} />
                      <span className="font-medium">{formatBRL(s.grossSalary)}</span>
                    </div>
                  </td>
                  <td className="px-5 py-3 text-right text-gray-700">
                    {formatBRL(s.netSalary)}
                  </td>
                  <td className="px-5 py-3 text-right text-gray-500">
                    {s.allowances ? formatBRL(s.allowances) : '—'}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

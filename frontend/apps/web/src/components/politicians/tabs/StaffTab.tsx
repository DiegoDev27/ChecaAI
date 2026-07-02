'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { transparencyApi } from '@checaai/api-client';
import { formatBRL } from '@/lib/utils';
import { Loader2, ChevronLeft, ChevronRight, Users } from 'lucide-react';

interface Props { id: number }

const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i);

export function StaffTab({ id }: Props) {
  const [page, setPage] = useState(1);
  const [year, setYear] = useState<number>(CURRENT_YEAR);
  const pageSize = 20;

  const { data, isLoading, isError } = useQuery({
    queryKey: ['politician-staff', id, year, page],
    queryFn: () => transparencyApi.cabinetStaff(id, year, undefined, page, pageSize),
    placeholderData: (prev) => prev,
  });

  if (isLoading) {
    return <div className="flex justify-center py-12"><Loader2 className="h-8 w-8 animate-spin text-brand-600" /></div>;
  }

  if (isError) {
    return <div className="text-center py-12 text-red-600 text-sm">Erro ao carregar assessores.</div>;
  }

  if (!data || data.totalCount === 0) {
    return (
      <div className="bg-white rounded-xl border p-8 text-center">
        <Users className="h-12 w-12 mx-auto mb-3 text-gray-200" />
        <div className="text-gray-400 text-sm">
          Nenhum dado de assessores disponível para {year}. Os dados são obtidos do Portal da Transparência (CGU).
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
          {data.totalCount} assessores registrados
        </span>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-gray-400 border-b bg-gray-50">
              <th className="px-5 py-2.5 font-medium">Nome</th>
              <th className="px-5 py-2.5 font-medium">Cargo</th>
              <th className="px-5 py-2.5 font-medium text-right">Salário bruto</th>
              <th className="px-5 py-2.5 font-medium text-right">Líquido</th>
              <th className="px-5 py-2.5 font-medium">Período</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {data.data.map((s, i) => (
              <tr key={`${s.id}-${i}`} className="hover:bg-gray-50 transition-colors">
                <td className="px-5 py-3 font-medium text-gray-800">{s.fullName}</td>
                <td className="px-5 py-3 text-gray-600 text-xs">{s.role ?? '—'}</td>
                <td className="px-5 py-3 text-right">
                  {s.grossSalary ? formatBRL(s.grossSalary) : '—'}
                </td>
                <td className="px-5 py-3 text-right text-gray-600">
                  {s.netSalary ? formatBRL(s.netSalary) : '—'}
                </td>
                <td className="px-5 py-3 text-gray-500 text-xs">
                  {s.month && s.year ? `${String(s.month).padStart(2,'0')}/${s.year}` : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
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

'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { sessionsApi } from '@checaai/api-client';
import Link from 'next/link';
import { formatDate, resultColor, cn } from '@/lib/utils';
import { AlertTriangle, ChevronRight } from 'lucide-react';

const PAGE_SIZE = 20;

export function SessionList() {
  const [chamber, setChamber] = useState('');
  const [hasAlert, setHasAlert] = useState<boolean | undefined>(undefined);
  const [page, setPage] = useState(1);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['sessions', chamber, hasAlert, page],
    queryFn: () =>
      sessionsApi.list({ chamber: chamber || undefined, hasAlert, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });

  return (
    <div className="space-y-5">
      {/* Filters */}
      <div className="bg-white rounded-xl border p-4 flex flex-wrap gap-3">
        <select
          value={chamber}
          onChange={(e) => { setChamber(e.target.value); setPage(1); }}
          className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500"
        >
          <option value="">Câmara + Senado</option>
          <option value="Câmara">Câmara dos Deputados</option>
          <option value="Senado">Senado Federal</option>
        </select>

        <select
          value={hasAlert === undefined ? '' : String(hasAlert)}
          onChange={(e) => {
            setHasAlert(e.target.value === '' ? undefined : e.target.value === 'true');
            setPage(1);
          }}
          className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500"
        >
          <option value="">Todas as votações</option>
          <option value="true">Com alerta ⚠️</option>
          <option value="false">Sem alerta</option>
        </select>
      </div>

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="bg-white rounded-xl border p-4 flex items-start gap-4 animate-pulse">
              <div className="flex-1 space-y-2">
                <div className="h-4 bg-slate-200 rounded w-full" />
                <div className="h-4 bg-slate-200 rounded w-3/4" />
                <div className="h-3 bg-slate-100 rounded w-1/3 mt-1" />
              </div>
              <div className="flex-shrink-0 space-y-1">
                <div className="h-4 bg-slate-100 rounded w-20" />
                <div className="h-4 bg-slate-100 rounded w-16" />
              </div>
            </div>
          ))}
        </div>
      )}

      {isError && (
        <div className="text-center py-12 text-red-600">
          Erro ao carregar votações.
        </div>
      )}

      {data && !isLoading && (
        <>
          <div className="text-sm text-slate-500">
            {data.totalCount.toLocaleString('pt-BR')} votações encontradas
          </div>

          <div className="space-y-2">
            {data.data.map((s) => (
              <Link
                key={s.id}
                href={`/votacoes/${s.id}`}
                className="group bg-white rounded-xl border hover:border-primary-300 hover:shadow-sm transition-all p-4 flex items-start gap-4"
              >
                {/* Alert badge */}
                {s.hasAlert && (
                  <AlertTriangle className="h-5 w-5 text-orange-500 flex-shrink-0 mt-0.5" />
                )}

                <div className="flex-1 min-w-0">
                  <div className="font-medium text-slate-900 group-hover:text-primary-700 leading-snug line-clamp-2">
                    {s.description}
                  </div>
                  <div className="flex flex-wrap gap-3 mt-2 text-xs text-slate-500">
                    <span>{s.chamber}</span>
                    <span>{formatDate(s.votingDate)}</span>
                    {s.proposalType && <span>{s.proposalType}</span>}
                  </div>
                </div>

                {/* Vote counts */}
                <div className="flex-shrink-0 text-right">
                  <div className="flex gap-2 text-xs font-medium">
                    <span className="text-green-700">{s.votesYes}✓</span>
                    <span className="text-red-700">{s.votesNo}✗</span>
                    {s.votesAbstention > 0 && (
                      <span className="text-orange-600">{s.votesAbstention}○</span>
                    )}
                  </div>
                  <span className={cn(
                    'text-xs mt-1 font-semibold px-2 py-0.5 rounded-full',
                    resultColor(s.result),
                  )}>
                    {s.result}
                  </span>
                </div>

                <ChevronRight className="h-4 w-4 text-slate-300 flex-shrink-0 self-center" />
              </Link>
            ))}
          </div>

          {/* Pagination */}
          {data.totalPages > 1 && (
            <div className="flex justify-center gap-2 pt-2">
              <button
                onClick={() => setPage(page - 1)}
                disabled={!data.hasPrevPage}
                className="px-4 py-2 border rounded-lg text-sm disabled:opacity-40 hover:bg-slate-50"
              >
                ← Anterior
              </button>
              <span className="px-4 py-2 text-sm text-slate-600">
                {page} / {data.totalPages}
              </span>
              <button
                onClick={() => setPage(page + 1)}
                disabled={!data.hasNextPage}
                className="px-4 py-2 border rounded-lg text-sm disabled:opacity-40 hover:bg-slate-50"
              >
                Próxima →
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

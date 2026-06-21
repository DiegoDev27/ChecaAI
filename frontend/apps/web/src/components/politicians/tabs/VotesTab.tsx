'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checa-ai/api-client';
import Link from 'next/link';
import { formatDate, voteColor, voteLabel, cn } from '@/lib/utils';
import { Loader2, ChevronLeft, ChevronRight } from 'lucide-react';

interface Props { id: number }

const VOTE_OPTIONS = [
  { value: '', label: 'Todos os votos' },
  { value: 'Yes', label: 'Sim' },
  { value: 'No', label: 'Não' },
  { value: 'Abstention', label: 'Abstenção' },
  { value: 'Absent', label: 'Ausente' },
];

export function VotesTab({ id }: Props) {
  const [page, setPage] = useState(1);
  const [voteFilter, setVoteFilter] = useState('');
  const pageSize = 25;

  const { data, isLoading, isError } = useQuery({
    queryKey: ['politician-votes', id, page],
    queryFn: () => politiciansApi.votes(id, page, pageSize),
    placeholderData: (prev) => prev,
  });

  // Client-side filter (API doesn't support vote-type filter)
  const displayData = voteFilter
    ? (data?.data ?? []).filter((v) => v.voteValue === voteFilter)
    : (data?.data ?? []);

  return (
    <div className="bg-white rounded-xl border overflow-hidden">
      {/* Filters */}
      <div className="p-4 border-b flex items-center gap-3 flex-wrap bg-gray-50">
        <select
          value={voteFilter}
          onChange={(e) => setVoteFilter(e.target.value)}
          className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
        >
          {VOTE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
        {data && (
          <span className="text-sm text-gray-500 ml-auto">
            {data.totalCount.toLocaleString('pt-BR')} votações no total
            {voteFilter && ` • ${displayData.length} nesta página`}
          </span>
        )}
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="flex justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-brand-600" />
        </div>
      ) : isError ? (
        <div className="text-center py-12 text-red-600 text-sm">Erro ao carregar votações.</div>
      ) : (
        <>
          <div className="divide-y">
            {displayData.map((v) => (
              <Link
                key={v.sessionId}
                href={`/votacoes/${v.sessionId}`}
                className="flex items-center gap-3 px-5 py-3.5 hover:bg-gray-50 transition-colors group"
              >
                <span className={cn(
                  'text-xs font-medium px-2 py-0.5 rounded-full flex-shrink-0 w-20 text-center',
                  voteColor(v.voteValue),
                )}>
                  {voteLabel(v.voteValue)}
                </span>
                <div className="flex-1 min-w-0">
                  <div className="text-sm text-gray-800 truncate group-hover:text-brand-700">
                    {v.description}
                  </div>
                  <div className="text-xs text-gray-400 mt-0.5">
                    {formatDate(v.votingDate)} • {v.chamber}
                  </div>
                </div>
                <span className={cn(
                  'text-xs px-2 py-0.5 rounded flex-shrink-0',
                  v.result.toLowerCase().includes('aprovad')
                    ? 'text-green-700 bg-green-50'
                    : 'text-red-700 bg-red-50',
                )}>
                  {v.result}
                </span>
              </Link>
            ))}
            {displayData.length === 0 && (
              <div className="text-center py-12 px-6">
                <svg className="h-10 w-10 mx-auto mb-3 text-gray-200" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                </svg>
                {voteFilter ? (
                  <p className="text-gray-400 text-sm">Nenhum voto do tipo &quot;{VOTE_OPTIONS.find(o => o.value === voteFilter)?.label}&quot; nesta página.</p>
                ) : data && data.totalCount === 0 ? (
                  <div>
                    <p className="text-gray-500 font-medium text-sm mb-1">Votos sendo sincronizados</p>
                    <p className="text-gray-400 text-sm">
                      Os votos nominais deste parlamentar estão sendo coletados das APIs da Câmara e do Senado.
                      Volte em alguns minutos.
                    </p>
                  </div>
                ) : (
                  <p className="text-gray-400 text-sm">Nenhuma votação encontrada.</p>
                )}
              </div>
            )}
          </div>

          {/* Pagination */}
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
  );
}

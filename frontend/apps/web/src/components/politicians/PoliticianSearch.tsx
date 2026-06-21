'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checa-ai/api-client';
import { BR_STATES, POLITICAL_POSITIONS } from '@checa-ai/types';
import { positionLabel, cn } from '@/lib/utils';
import { useDebounce } from '@/lib/useDebounce';
import { PoliticianCard } from './PoliticianCard';
import { Search, ChevronLeft, ChevronRight } from 'lucide-react';

const PAGE_SIZE = 20;

export function PoliticianSearch() {
  const [search, setSearch]     = useState('');
  const [position, setPosition] = useState('');
  const [state, setState]       = useState('');
  const [party, setParty]       = useState('');
  const [page, setPage]         = useState(1);

  const debouncedQ = useDebounce(search, 350);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['politicians', debouncedQ, position, state, party, page],
    queryFn: () =>
      politiciansApi.list({
        q: debouncedQ || undefined,
        position: position || undefined,
        state: state || undefined,
        party: party || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: (prev) => prev,
  });

  const resetFilters = () => {
    setSearch('');
    setPosition('');
    setState('');
    setParty('');
    setPage(1);
  };

  return (
    <div className="space-y-6">
      {/* Filters */}
      <div className="bg-white rounded-xl border p-4 space-y-4">
        {/* Search input */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            placeholder="Buscar por nome..."
            className="w-full pl-10 pr-4 py-2.5 border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
          />
        </div>

        {/* Filter row */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <select
            value={position}
            onChange={(e) => { setPosition(e.target.value); setPage(1); }}
            className="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500 bg-white"
          >
            <option value="">Todos os cargos</option>
            {POLITICAL_POSITIONS.map((p) => (
              <option key={p} value={p}>{positionLabel(p)}</option>
            ))}
          </select>

          <select
            value={state}
            onChange={(e) => { setState(e.target.value); setPage(1); }}
            className="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500 bg-white"
          >
            <option value="">Todos os estados</option>
            {BR_STATES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>

          <input
            type="text"
            value={party}
            onChange={(e) => { setParty(e.target.value.toUpperCase()); setPage(1); }}
            placeholder="Partido (PT, PL...)"
            maxLength={15}
            className="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
          />

          <button
            onClick={resetFilters}
            className="text-sm text-gray-500 hover:text-gray-800 border rounded-lg px-3 py-2 transition-colors hover:bg-gray-50"
          >
            Limpar filtros
          </button>
        </div>
      </div>

      {/* Results header */}
      {data && (
        <div className="flex items-center justify-between text-sm text-gray-600">
          <span>
            {data.totalCount.toLocaleString('pt-BR')} parlamentar{data.totalCount !== 1 ? 'es' : ''} encontrado{data.totalCount !== 1 ? 's' : ''}
          </span>
          {data.totalPages > 1 && (
            <span>Página {data.page} de {data.totalPages}</span>
          )}
        </div>
      )}

      {/* Loading skeletons */}
      {isLoading && (
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {Array.from({ length: 12 }).map((_, i) => (
            <div key={i} className="bg-white rounded-xl border overflow-hidden animate-pulse">
              <div className="h-36 bg-gray-100" />
              <div className="p-4 space-y-2">
                <div className="h-4 bg-gray-200 rounded w-3/4" />
                <div className="h-3 bg-gray-100 rounded w-1/2" />
                <div className="h-5 bg-gray-100 rounded w-1/3 mt-2" />
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Error */}
      {isError && (
        <div className="text-center py-12 text-red-600">
          Erro ao carregar parlamentares. Verifique se a API está rodando.
        </div>
      )}

      {/* Results grid */}
      {data && !isLoading && (
        <>
          {data.data.length === 0 ? (
            <div className="text-center py-16 text-gray-500">
              Nenhum parlamentar encontrado com esses filtros.
            </div>
          ) : (
            <div className="grid sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
              {data.data.map((p) => (
                <PoliticianCard key={p.id} politician={p} />
              ))}
            </div>
          )}

          {/* Pagination */}
          {data.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 pt-4">
              <button
                onClick={() => setPage(page - 1)}
                disabled={!data.hasPrevPage}
                className={cn(
                  'p-2 rounded-lg border transition-colors',
                  data.hasPrevPage
                    ? 'hover:bg-gray-100 text-gray-700'
                    : 'opacity-40 cursor-not-allowed text-gray-400',
                )}
              >
                <ChevronLeft className="h-4 w-4" />
              </button>

              {/* Page numbers */}
              {Array.from({ length: Math.min(5, data.totalPages) }, (_, i) => {
                const p = Math.max(1, Math.min(data.totalPages - 4, page - 2)) + i;
                return (
                  <button
                    key={p}
                    onClick={() => setPage(p)}
                    className={cn(
                      'w-9 h-9 rounded-lg text-sm font-medium border transition-colors',
                      p === page
                        ? 'bg-brand-600 text-white border-brand-600'
                        : 'hover:bg-gray-100 text-gray-700',
                    )}
                  >
                    {p}
                  </button>
                );
              })}

              <button
                onClick={() => setPage(page + 1)}
                disabled={!data.hasNextPage}
                className={cn(
                  'p-2 rounded-lg border transition-colors',
                  data.hasNextPage
                    ? 'hover:bg-gray-100 text-gray-700'
                    : 'opacity-40 cursor-not-allowed text-gray-400',
                )}
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

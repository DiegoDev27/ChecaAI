'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { proposalsApi } from '@checa-ai/api-client';
import Link from 'next/link';
import { formatDate, cn } from '@/lib/utils';
import { Loader2, ChevronLeft, ChevronRight, FileText, Search } from 'lucide-react';
import { useDebounce } from '@/lib/useDebounce';

const PROPOSAL_TYPES = ['PL', 'PEC', 'PLP', 'MPV', 'PLV', 'PDL', 'PDS', 'MSC'];
const CHAMBERS = ['Câmara dos Deputados', 'Senado Federal'];
const STATUSES = ['Em tramitação', 'Aprovado', 'Rejeitado', 'Arquivado', 'Vetado'];
const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 10 }, (_, i) => CURRENT_YEAR - i);

const STATUS_STYLE: Record<string, string> = {
  'Aprovado': 'bg-green-100 text-green-700',
  'Rejeitado': 'bg-red-100 text-red-700',
  'Em tramitação': 'bg-blue-100 text-blue-700',
  'Arquivado': 'bg-gray-100 text-gray-600',
  'Vetado': 'bg-orange-100 text-orange-700',
};

export function ProposalList() {
  const [q, setQ] = useState('');
  const [type, setType] = useState('');
  const [year, setYear] = useState<number | undefined>(undefined);
  const [chamber, setChamber] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const debouncedQ = useDebounce(q, 400);
  const pageSize = 20;

  const { data, isLoading, isError } = useQuery({
    queryKey: ['proposals', debouncedQ, type, year, chamber, status, page],
    queryFn: () =>
      proposalsApi.list({ q: debouncedQ, type, year, chamber, status, page, pageSize }),
    placeholderData: (prev) => prev,
  });

  const resetFilters = () => {
    setQ('');
    setType('');
    setYear(undefined);
    setChamber('');
    setStatus('');
    setPage(1);
  };

  const hasFilters = q || type || year || chamber || status;

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="bg-white rounded-xl border p-4 space-y-3">
        {/* Search */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
          <input
            type="text"
            value={q}
            onChange={(e) => { setQ(e.target.value); setPage(1); }}
            placeholder="Buscar por título, ementa ou autor..."
            className="w-full pl-10 pr-4 py-2.5 border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
          />
        </div>

        {/* Filter row */}
        <div className="flex flex-wrap gap-2">
          <select
            value={type}
            onChange={(e) => { setType(e.target.value); setPage(1); }}
            className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="">Tipo</option>
            {PROPOSAL_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>

          <select
            value={year ?? ''}
            onChange={(e) => { setYear(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
            className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="">Ano</option>
            {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>

          <select
            value={chamber}
            onChange={(e) => { setChamber(e.target.value); setPage(1); }}
            className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="">Câmara</option>
            {CHAMBERS.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>

          <select
            value={status}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }}
            className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="">Status</option>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>

          {hasFilters && (
            <button
              onClick={resetFilters}
              className="text-sm text-gray-500 hover:text-gray-800 px-3 py-2 border rounded-lg hover:bg-gray-50 transition-colors"
            >
              Limpar filtros
            </button>
          )}

          {data && (
            <span className="ml-auto text-sm text-gray-500 self-center">
              {data.totalCount.toLocaleString('pt-BR')} proposições
            </span>
          )}
        </div>
      </div>

      {/* Results */}
      {isLoading ? (
        <div className="flex justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-brand-600" />
        </div>
      ) : isError ? (
        <div className="text-center py-12 text-red-600">Erro ao carregar proposições.</div>
      ) : (
        <>
          <div className="space-y-2">
            {(data?.data ?? []).map((p) => (
              <Link
                key={p.id}
                href={`/proposicoes/${p.id}`}
                className="block bg-white rounded-xl border hover:border-brand-300 hover:shadow-sm transition-all p-4 group"
              >
                <div className="flex items-start gap-3">
                  <FileText className="h-5 w-5 text-gray-300 flex-shrink-0 mt-0.5 group-hover:text-brand-400 transition-colors" />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap mb-1">
                      <span className="text-xs font-bold text-brand-700 bg-brand-50 px-2 py-0.5 rounded">
                        {p.type} {p.number && `${p.number}/`}{p.year}
                      </span>
                      <span className={cn(
                        'text-xs font-medium px-2 py-0.5 rounded',
                        STATUS_STYLE[p.status] ?? 'bg-gray-100 text-gray-600',
                      )}>
                        {p.status}
                      </span>
                      <span className="text-xs text-gray-400">{p.chamber}</span>
                      {p.votingSessionCount > 0 && (
                        <span className="text-xs text-civic-600">
                          {p.votingSessionCount} votação{p.votingSessionCount > 1 ? 'ões' : ''}
                        </span>
                      )}
                    </div>

                    <h3 className="text-sm font-semibold text-gray-900 leading-snug group-hover:text-brand-700 transition-colors line-clamp-2">
                      {p.title}
                    </h3>

                    {p.summary && (
                      <p className="text-xs text-gray-500 mt-1 line-clamp-2 leading-relaxed">
                        {p.summary}
                      </p>
                    )}

                    <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
                      {p.author && <span>Autor: <span className="text-gray-600">{p.author}</span></span>}
                      {p.proposalDate && <span>• {formatDate(p.proposalDate)}</span>}
                    </div>
                  </div>
                </div>
              </Link>
            ))}

            {(data?.data ?? []).length === 0 && (
              <div className="text-center py-16 text-gray-400">
                <FileText className="h-12 w-12 mx-auto mb-3 text-gray-200" />
                <p>Nenhuma proposição encontrada.</p>
                {hasFilters && (
                  <button onClick={resetFilters} className="mt-3 text-sm text-brand-600 hover:underline">
                    Limpar filtros
                  </button>
                )}
              </div>
            )}
          </div>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between py-3">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={!data.hasPrevPage}
                className="flex items-center gap-1 text-sm px-4 py-2 rounded-lg border disabled:opacity-40 disabled:cursor-not-allowed hover:bg-white transition-colors bg-white"
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
                className="flex items-center gap-1 text-sm px-4 py-2 rounded-lg border disabled:opacity-40 disabled:cursor-not-allowed hover:bg-white transition-colors bg-white"
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

'use client';

import { useQuery } from '@tanstack/react-query';
import { proposalsApi } from '@checa-ai/api-client';
import Link from 'next/link';
import { formatDate, cn } from '@/lib/utils';
import {
  ChevronRight, FileText, Calendar, User,
  Building2, CheckCircle2, XCircle, MinusCircle, Vote,
} from 'lucide-react';

interface Props { id: number }

const STATUS_STYLE: Record<string, string> = {
  'Aprovado': 'bg-green-100 text-green-800 border-green-200',
  'Rejeitado': 'bg-red-100 text-red-800 border-red-200',
  'Em tramitação': 'bg-blue-100 text-blue-800 border-blue-200',
  'Arquivado': 'bg-gray-100 text-gray-700 border-gray-200',
  'Vetado': 'bg-orange-100 text-orange-800 border-orange-200',
};

function ResultBadge({ result }: { result: string }) {
  const approved = result.toLowerCase().includes('aprovad');
  const rejected = result.toLowerCase().includes('rejeitad');

  return (
    <span className={cn(
      'text-xs font-bold px-2.5 py-1 rounded-full inline-flex items-center gap-1',
      approved ? 'bg-green-100 text-green-800' : rejected ? 'bg-red-100 text-red-800' : 'bg-gray-100 text-gray-700',
    )}>
      {approved ? <CheckCircle2 className="h-3.5 w-3.5" /> : rejected ? <XCircle className="h-3.5 w-3.5" /> : <MinusCircle className="h-3.5 w-3.5" />}
      {result}
    </span>
  );
}

export function ProposalDetail({ id }: Props) {
  const { data: p, isLoading, isError } = useQuery({
    queryKey: ['proposal', id],
    queryFn: () => proposalsApi.get(id),
  });

  if (isLoading) {
    return (
      <div className="space-y-5 animate-pulse">
        <div className="h-4 bg-gray-200 rounded w-48" />
        <div className="bg-white rounded-xl border p-6 space-y-4">
          <div className="h-6 bg-gray-200 rounded w-3/4" />
          <div className="h-4 bg-gray-100 rounded w-1/2" />
          <div className="h-4 bg-gray-100 rounded w-full" />
          <div className="h-4 bg-gray-100 rounded w-5/6" />
        </div>
      </div>
    );
  }

  if (isError || !p) {
    return <div className="text-center py-16 text-red-600">Proposição não encontrada.</div>;
  }

  return (
    <div className="space-y-6">
      {/* Breadcrumb */}
      <nav className="text-sm text-gray-500 flex items-center gap-1 flex-wrap">
        <Link href="/proposicoes" className="hover:text-brand-600">Proposições</Link>
        <ChevronRight className="h-3 w-3" />
        <span className="text-gray-800">
          {p.type} {p.number && `${p.number}/`}{p.year}
        </span>
      </nav>

      {/* Header card */}
      <div className="bg-white rounded-xl border p-6">
        <div className="flex flex-wrap items-center gap-2 mb-3">
          <span className="text-sm font-bold text-brand-700 bg-brand-50 border border-brand-200 px-3 py-1 rounded-lg">
            {p.type} {p.number && `${p.number}/`}{p.year}
          </span>
          <span className={cn(
            'text-xs font-medium px-2.5 py-1 rounded-full border',
            STATUS_STYLE[p.status] ?? 'bg-gray-100 text-gray-700 border-gray-200',
          )}>
            {p.status}
          </span>
        </div>

        <h1 className="text-xl font-bold text-gray-900 leading-snug mb-4">{p.title}</h1>

        {p.summary && (
          <div className="bg-gray-50 rounded-lg p-4 border text-sm text-gray-700 leading-relaxed mb-4">
            <p className="font-medium text-xs text-gray-500 mb-2 uppercase tracking-wide">Ementa</p>
            <p>{p.summary}</p>
          </div>
        )}

        {/* Meta grid */}
        <div className="grid sm:grid-cols-2 md:grid-cols-3 gap-3 text-sm">
          {p.author && (
            <div className="flex items-center gap-2 text-gray-600">
              <User className="h-4 w-4 text-gray-400" />
              <span className="truncate">Autor: <span className="font-medium text-gray-800">{p.author}</span></span>
            </div>
          )}
          <div className="flex items-center gap-2 text-gray-600">
            <Building2 className="h-4 w-4 text-gray-400" />
            <span>{p.chamber}</span>
          </div>
          {p.proposalDate && (
            <div className="flex items-center gap-2 text-gray-600">
              <Calendar className="h-4 w-4 text-gray-400" />
              <span>Apresentada em {formatDate(p.proposalDate)}</span>
            </div>
          )}
        </div>
      </div>

      {/* Voting sessions */}
      {p.votingSessions && p.votingSessions.length > 0 ? (
        <div className="bg-white rounded-xl border overflow-hidden">
          <div className="px-5 py-4 border-b bg-gray-50 flex items-center gap-2">
            <Vote className="h-4 w-4 text-brand-600" />
            <h2 className="font-semibold text-gray-800">
              Votações relacionadas ({p.votingSessions.length})
            </h2>
          </div>
          <div className="divide-y">
            {p.votingSessions.map((s) => {
              const approved = s.result?.toLowerCase().includes('aprovad');
              return (
                <Link
                  key={s.id}
                  href={`/votacoes/${s.id}`}
                  className="flex items-start gap-4 px-5 py-4 hover:bg-gray-50 transition-colors group"
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap mb-1">
                      <ResultBadge result={s.result ?? ''} />
                      <span className="text-xs text-gray-400">{s.chamber}</span>
                      {s.sessionType && (
                        <span className="text-xs text-gray-400">• {s.sessionType}</span>
                      )}
                    </div>
                    <p className="text-sm text-gray-800 leading-snug group-hover:text-brand-700 line-clamp-2">
                      {s.description}
                    </p>
                    <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
                      <span>{formatDate(s.votingDate)}</span>
                      <span>• {s.totalVotes} votos</span>
                      <span className="text-green-600">↑ {s.votesYes} Sim</span>
                      <span className="text-red-600">↓ {s.votesNo} Não</span>
                    </div>
                  </div>
                  <ChevronRight className="h-4 w-4 text-gray-300 flex-shrink-0 mt-1 group-hover:text-brand-500 transition-colors" />
                </Link>
              );
            })}
          </div>
        </div>
      ) : (
        <div className="bg-white rounded-xl border p-6 text-center text-gray-400 text-sm">
          <Vote className="h-10 w-10 mx-auto mb-2 text-gray-200" />
          Nenhuma votação registrada para esta proposição.
        </div>
      )}
    </div>
  );
}

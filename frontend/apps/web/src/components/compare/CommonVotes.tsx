'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checaai/api-client';
import Link from 'next/link';
import { formatDate, voteColor, voteLabel, cn } from '@/lib/utils';
import { Loader2, CheckCircle2, XCircle, ThumbsUp, ThumbsDown } from 'lucide-react';

interface Props {
  id1: number;
  id2: number;
  name1: string;
  name2: string;
}

interface CommonVote {
  sessionId: number;
  description: string;
  votingDate: string;
  chamber: string;
  result: string;
  vote1: string;
  vote2: string;
  agree: boolean;
}

const PAGE_SIZE = 100;

export function CommonVotes({ id1, id2, name1, name2 }: Props) {
  const { data: votes1, isLoading: l1 } = useQuery({
    queryKey: ['compare-votes', id1],
    queryFn: () => politiciansApi.votes(id1, 1, PAGE_SIZE),
    staleTime: 5 * 60 * 1000,
  });

  const { data: votes2, isLoading: l2 } = useQuery({
    queryKey: ['compare-votes', id2],
    queryFn: () => politiciansApi.votes(id2, 1, PAGE_SIZE),
    staleTime: 5 * 60 * 1000,
  });

  const { common, agreeCount, disagreeCount } = useMemo(() => {
    if (!votes1?.data || !votes2?.data) return { common: [], agreeCount: 0, disagreeCount: 0 };

    const map2 = new Map(votes2.data.map((v) => [v.sessionId, v]));
    const result: CommonVote[] = [];
    let agree = 0;
    let disagree = 0;

    for (const v1 of votes1.data) {
      const v2 = map2.get(v1.sessionId);
      if (!v2) continue;

      const agreed = v1.voteValue === v2.voteValue;
      if (agreed) agree++;
      else disagree++;

      result.push({
        sessionId: v1.sessionId,
        description: v1.description,
        votingDate: v1.votingDate,
        chamber: v1.chamber,
        result: v1.result,
        vote1: v1.voteValue,
        vote2: v2.voteValue,
        agree: agreed,
      });
    }

    return { common: result, agreeCount: agree, disagreeCount: disagree };
  }, [votes1, votes2]);

  if (l1 || l2) {
    return (
      <div className="bg-white rounded-xl border p-8 flex items-center justify-center gap-3 text-slate-500">
        <Loader2 className="h-5 w-5 animate-spin text-primary-600" />
        Carregando votos em comum...
      </div>
    );
  }

  if (common.length === 0) {
    return (
      <div className="bg-white rounded-xl border p-8 text-center text-slate-400">
        <div className="text-sm">Nenhuma votação em comum encontrada nas últimas {PAGE_SIZE} votações de cada parlamentar.</div>
      </div>
    );
  }

  const agreePct = common.length > 0 ? ((agreeCount / common.length) * 100).toFixed(0) : 0;

  return (
    <div className="space-y-4">
      {/* Summary */}
      <div className="bg-white rounded-xl border p-5">
        <h3 className="font-semibold text-slate-800 mb-4">
          {common.length} votações em comum (últimas {PAGE_SIZE} de cada)
        </h3>
        <div className="grid grid-cols-3 gap-4 text-center">
          <div>
            <div className="text-3xl font-bold text-primary-700">{agreePct}%</div>
            <div className="text-sm text-slate-500 mt-1">Concordância</div>
          </div>
          <div>
            <div className="text-3xl font-bold text-green-700">{agreeCount}</div>
            <div className="text-sm text-slate-500 mt-1">Votaram igual</div>
          </div>
          <div>
            <div className="text-3xl font-bold text-red-700">{disagreeCount}</div>
            <div className="text-sm text-slate-500 mt-1">Votaram diferente</div>
          </div>
        </div>

        {/* Agreement bar */}
        <div className="mt-4 h-3 bg-red-100 rounded-full overflow-hidden">
          <div
            className="h-full bg-green-500 rounded-full transition-all duration-500"
            style={{ width: `${agreePct}%` }}
          />
        </div>
        <div className="flex justify-between text-xs text-slate-400 mt-1">
          <span>Discordância</span>
          <span>Concordância total</span>
        </div>
      </div>

      {/* Vote list */}
      <div className="bg-white rounded-xl border overflow-hidden">
        <div className="px-5 py-3 border-b bg-slate-50 grid grid-cols-5 gap-2 text-xs font-medium text-slate-400">
          <div className="col-span-3">Votação</div>
          <div className="text-center">{name1.split(' ')[0]}</div>
          <div className="text-center">{name2.split(' ')[0]}</div>
        </div>

        <div className="divide-y max-h-[480px] overflow-y-auto">
          {common.slice(0, 50).map((v) => (
            <Link
              key={v.sessionId}
              href={`/votacoes/${v.sessionId}`}
              className={cn(
                'grid grid-cols-5 gap-2 px-5 py-3 hover:bg-slate-50 transition-colors items-center',
                v.agree ? '' : 'bg-red-50/30 hover:bg-red-50',
              )}
            >
              <div className="col-span-3 min-w-0">
                <div className="flex items-center gap-2 mb-0.5">
                  {v.agree ? (
                    <CheckCircle2 className="h-3.5 w-3.5 text-green-500 flex-shrink-0" />
                  ) : (
                    <XCircle className="h-3.5 w-3.5 text-red-400 flex-shrink-0" />
                  )}
                  <span className="text-xs text-slate-400">{formatDate(v.votingDate)} • {v.chamber}</span>
                </div>
                <div className="text-sm text-slate-800 truncate leading-snug">{v.description}</div>
              </div>
              <div className="text-center">
                <span className={cn(
                  'text-xs font-medium px-2 py-0.5 rounded-full',
                  voteColor(v.vote1),
                )}>
                  {voteLabel(v.vote1)}
                </span>
              </div>
              <div className="text-center">
                <span className={cn(
                  'text-xs font-medium px-2 py-0.5 rounded-full',
                  voteColor(v.vote2),
                )}>
                  {voteLabel(v.vote2)}
                </span>
              </div>
            </Link>
          ))}
        </div>

        {common.length > 50 && (
          <div className="px-5 py-3 border-t bg-slate-50 text-center text-xs text-slate-500">
            Exibindo 50 de {common.length} votações em comum
          </div>
        )}
      </div>
    </div>
  );
}

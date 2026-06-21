'use client';

import type { PoliticianDetail } from '@checa-ai/types';
import Link from 'next/link';
import { formatBRL, formatDate, voteColor, voteLabel, cn } from '@/lib/utils';
import { Vote, DollarSign, TrendingUp, Briefcase } from 'lucide-react';
import { VoteStatsBar } from '../VoteStatsBar';

interface Props {
  p: PoliticianDetail;
  id: number;
}

export function OverviewTab({ p, id }: Props) {
  const hasAnyData =
    p.voteStats ||
    p.latestSalary ||
    p.expenseSummary ||
    (p.committees && p.committees.length > 0) ||
    (p.recentVotes && p.recentVotes.length > 0);

  if (!hasAnyData) {
    return (
      <div className="space-y-4">
        {/* Info box explaining sync status */}
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-5 flex gap-4">
          <div className="flex-shrink-0 mt-0.5">
            <svg className="h-5 w-5 text-amber-500" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495zM10 5a.75.75 0 01.75.75v3.5a.75.75 0 01-1.5 0v-3.5A.75.75 0 0110 5zm0 9a1 1 0 100-2 1 1 0 000 2z" clipRule="evenodd" />
            </svg>
          </div>
          <div>
            <p className="text-amber-800 font-medium text-sm">Dados em sincronização</p>
            <p className="text-amber-700 text-sm mt-1">
              As informações de votos, despesas e salários deste parlamentar estão sendo coletadas automaticamente.
              Volte em alguns minutos para ver os dados atualizados.
            </p>
          </div>
        </div>

        {/* What we're collecting */}
        <div className="rounded-xl border bg-white p-5">
          <h3 className="text-sm font-semibold text-gray-700 mb-3">O que estamos coletando:</h3>
          <ul className="space-y-2 text-sm text-gray-500">
            {[
              { label: 'Votos nominais em plenário', icon: '🗳️' },
              { label: 'Despesas de cota parlamentar (CEAP)', icon: '💳' },
              { label: 'Salários e subsídios (Portal da Transparência)', icon: '💰' },
              { label: 'Auxílios (moradia, saúde, educação)', icon: '🏠' },
              { label: 'Presença em sessões', icon: '📋' },
              { label: 'Comissões e frentes parlamentares', icon: '🏛️' },
            ].map((item) => (
              <li key={item.label} className="flex items-center gap-2">
                <span>{item.icon}</span>
                <span>{item.label}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Vote stats */}
      {p.voteStats && (
        <div className="bg-white rounded-xl border p-5">
          <h2 className="font-semibold text-gray-800 flex items-center gap-2 mb-4">
            <Vote className="h-4 w-4 text-brand-600" />
            Estatísticas de voto
          </h2>
          <VoteStatsBar stats={p.voteStats} />
          <div className="mt-4 grid grid-cols-4 gap-3 text-center text-sm">
            {[
              { label: 'Sim', value: p.voteStats.yes, cls: 'text-green-700' },
              { label: 'Não', value: p.voteStats.no, cls: 'text-red-700' },
              { label: 'Abstenção', value: p.voteStats.abstention, cls: 'text-orange-700' },
              { label: 'Ausente', value: p.voteStats.absent, cls: 'text-gray-600' },
            ].map((s) => (
              <div key={s.label}>
                <div className={cn('text-xl font-bold', s.cls)}>{s.value}</div>
                <div className="text-xs text-gray-500 mt-0.5">{s.label}</div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Latest salary */}
      {p.latestSalary && (
        <div className="bg-white rounded-xl border p-5">
          <h2 className="font-semibold text-gray-800 flex items-center gap-2 mb-4">
            <DollarSign className="h-4 w-4 text-brand-600" />
            Último salário registrado
          </h2>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <div className="text-xs text-gray-500 mb-1">Bruto</div>
              <div className="text-lg font-bold text-gray-900">
                {formatBRL(p.latestSalary.grossSalary)}
              </div>
            </div>
            <div>
              <div className="text-xs text-gray-500 mb-1">Líquido</div>
              <div className="text-lg font-bold text-gray-900">
                {formatBRL(p.latestSalary.netSalary)}
              </div>
            </div>
            <div>
              <div className="text-xs text-gray-500 mb-1">Período</div>
              <div className="text-lg font-bold text-gray-900">
                {String(p.latestSalary.month).padStart(2, '0')}/{p.latestSalary.year}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Expense summary */}
      {p.expenseSummary && (
        <div className="bg-white rounded-xl border p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-gray-800 flex items-center gap-2">
              <TrendingUp className="h-4 w-4 text-brand-600" />
              Cota parlamentar (CEAP) — {p.expenseSummary.year}
            </h2>
          </div>
          <div className="flex items-baseline gap-3 mb-4">
            <span className="text-3xl font-bold text-gray-900">
              {formatBRL(p.expenseSummary.total)}
            </span>
            <span className="text-sm text-gray-500">{p.expenseSummary.count} lançamentos</span>
          </div>
          <div className="space-y-2">
            {p.expenseSummary.byCategory.slice(0, 6).map((c) => {
              const pct = p.expenseSummary
                ? ((c.total / p.expenseSummary.total) * 100).toFixed(0)
                : 0;
              return (
                <div key={c.category}>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-600 truncate mr-3">{c.category}</span>
                    <span className="font-medium text-gray-900 flex-shrink-0">
                      {formatBRL(c.total)}
                    </span>
                  </div>
                  <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-brand-400 rounded-full"
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Committees */}
      {p.committees && p.committees.length > 0 && (
        <div className="bg-white rounded-xl border p-5">
          <h2 className="font-semibold text-gray-800 flex items-center gap-2 mb-4">
            <Briefcase className="h-4 w-4 text-brand-600" />
            Comissões ({p.committees.length})
          </h2>
          <div className="grid sm:grid-cols-2 gap-2">
            {p.committees.map((c) => (
              <div key={c.committeeId} className="text-sm border rounded-lg p-3 bg-gray-50">
                <div className="font-medium text-gray-800 leading-tight">{c.committeeName}</div>
                <div className="text-xs text-gray-500 mt-1">
                  {c.chamber} • <span className="text-brand-700">{c.role}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Recent votes */}
      {p.recentVotes && p.recentVotes.length > 0 && (
        <div className="bg-white rounded-xl border p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-gray-800 flex items-center gap-2">
              <Vote className="h-4 w-4 text-brand-600" />
              Últimas votações
            </h2>
          </div>
          <div className="space-y-1.5">
            {p.recentVotes.map((v) => (
              <Link
                key={v.sessionId}
                href={`/votacoes/${v.sessionId}`}
                className="flex items-center gap-3 p-3 rounded-lg hover:bg-gray-50 transition-colors group"
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
          </div>
        </div>
      )}
    </div>
  );
}

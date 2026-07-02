'use client';

import type { PoliticianDetail } from '@checaai/types';
import { formatBRL, presenceBadgeColor, cn } from '@/lib/utils';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';

interface Props {
  p1: PoliticianDetail;
  p2: PoliticianDetail;
}

function StatRow({
  label,
  v1,
  v2,
  format = (v: number) => String(v),
  higherIsBetter = true,
}: {
  label: string;
  v1: number | null;
  v2: number | null;
  format?: (v: number) => string;
  higherIsBetter?: boolean;
}) {
  const bothHave = v1 !== null && v2 !== null;
  const p1Wins = bothHave && (higherIsBetter ? v1! > v2! : v1! < v2!);
  const p2Wins = bothHave && (higherIsBetter ? v2! > v1! : v2! < v1!);

  return (
    <div className="grid grid-cols-3 gap-2 items-center py-3 border-b last:border-0">
      <div className={cn(
        'text-right text-sm font-semibold',
        p1Wins ? 'text-brand-700' : 'text-gray-700',
      )}>
        {v1 !== null ? format(v1) : '—'}
        {p1Wins && <span className="ml-1 text-brand-500">↑</span>}
      </div>
      <div className="text-center text-xs text-gray-500 font-medium">{label}</div>
      <div className={cn(
        'text-left text-sm font-semibold',
        p2Wins ? 'text-civic-700' : 'text-gray-700',
      )}>
        {p2Wins && <span className="mr-1 text-civic-500">↑</span>}
        {v2 !== null ? format(v2) : '—'}
      </div>
    </div>
  );
}

export function CompareStats({ p1, p2 }: Props) {
  const vs1 = p1.voteStats;
  const vs2 = p2.voteStats;

  const pct = (n: number, total: number) =>
    total > 0 ? `${((n / total) * 100).toFixed(1)}%` : '—';

  return (
    <div className="bg-white rounded-xl border overflow-hidden">
      {/* Header */}
      <div className="grid grid-cols-3 gap-4 bg-gray-50 border-b px-4 py-3">
        <div className="text-right">
          <div className="text-xs font-bold text-brand-700 uppercase tracking-wide">
            {p1.fullName.split(' ')[0]}
          </div>
          <div className="text-xs text-gray-500">{p1.party}</div>
        </div>
        <div className="text-center text-xs text-gray-400 font-medium self-center">vs</div>
        <div className="text-left">
          <div className="text-xs font-bold text-civic-700 uppercase tracking-wide">
            {p2.fullName.split(' ')[0]}
          </div>
          <div className="text-xs text-gray-500">{p2.party}</div>
        </div>
      </div>

      <div className="px-4">
        {/* Vote stats */}
        {vs1 && vs2 && (
          <>
            <StatRow
              label="Total de votos"
              v1={vs1.total}
              v2={vs2.total}
              format={(v) => v.toLocaleString('pt-BR')}
            />
            <StatRow
              label="Presença %"
              v1={vs1.presenceRate}
              v2={vs2.presenceRate}
              format={(v) => `${v.toFixed(1)}%`}
            />
            <StatRow
              label="Sim %"
              v1={vs1.total > 0 ? (vs1.yes / vs1.total) * 100 : null}
              v2={vs2.total > 0 ? (vs2.yes / vs2.total) * 100 : null}
              format={(v) => `${v.toFixed(1)}%`}
            />
            <StatRow
              label="Não %"
              v1={vs1.total > 0 ? (vs1.no / vs1.total) * 100 : null}
              v2={vs2.total > 0 ? (vs2.no / vs2.total) * 100 : null}
              format={(v) => `${v.toFixed(1)}%`}
              higherIsBetter={false}
            />
            <StatRow
              label="Ausente %"
              v1={vs1.total > 0 ? (vs1.absent / vs1.total) * 100 : null}
              v2={vs2.total > 0 ? (vs2.absent / vs2.total) * 100 : null}
              format={(v) => `${v.toFixed(1)}%`}
              higherIsBetter={false}
            />
          </>
        )}

        {/* Salary */}
        {(p1.latestSalary || p2.latestSalary) && (
          <StatRow
            label="Salário bruto"
            v1={p1.latestSalary?.grossSalary ?? null}
            v2={p2.latestSalary?.grossSalary ?? null}
            format={formatBRL}
            higherIsBetter={false}
          />
        )}

        {/* Expenses */}
        {(p1.expenseSummary || p2.expenseSummary) && (
          <StatRow
            label="Despesas CEAP"
            v1={p1.expenseSummary?.total ?? null}
            v2={p2.expenseSummary?.total ?? null}
            format={formatBRL}
            higherIsBetter={false}
          />
        )}

        {/* Committees */}
        <StatRow
          label="Comissões"
          v1={p1.committees?.length ?? 0}
          v2={p2.committees?.length ?? 0}
          format={(v) => String(v)}
        />
      </div>
    </div>
  );
}

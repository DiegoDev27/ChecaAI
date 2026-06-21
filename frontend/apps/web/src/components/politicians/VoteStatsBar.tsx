'use client';

import type { VoteStats } from '@checa-ai/types';
import { cn } from '@/lib/utils';

interface Props { stats: VoteStats }

export function VoteStatsBar({ stats }: Props) {
  const total = stats.total || 1;

  const segments = [
    { label: 'Sim',       value: stats.yes,        pct: stats.yes / total * 100,        color: 'bg-green-500' },
    { label: 'Não',       value: stats.no,         pct: stats.no / total * 100,         color: 'bg-red-500' },
    { label: 'Abstenção', value: stats.abstention,  pct: stats.abstention / total * 100, color: 'bg-orange-400' },
    { label: 'Ausente',   value: stats.absent,     pct: stats.absent / total * 100,     color: 'bg-gray-300' },
  ];

  return (
    <div className="space-y-3">
      {/* Stacked bar */}
      <div className="flex h-4 rounded-full overflow-hidden gap-0.5">
        {segments.map((s) =>
          s.pct > 0 ? (
            <div
              key={s.label}
              title={`${s.label}: ${s.value} (${s.pct.toFixed(1)}%)`}
              className={cn('transition-all', s.color)}
              style={{ width: `${s.pct}%` }}
            />
          ) : null,
        )}
      </div>

      {/* Legend */}
      <div className="grid grid-cols-2 gap-x-4 gap-y-1">
        {segments.map((s) => (
          <div key={s.label} className="flex items-center justify-between text-sm">
            <div className="flex items-center gap-2">
              <div className={cn('w-2.5 h-2.5 rounded-full', s.color)} />
              <span className="text-gray-600">{s.label}</span>
            </div>
            <span className="font-medium text-gray-800">
              {s.value.toLocaleString('pt-BR')} ({s.pct.toFixed(1)}%)
            </span>
          </div>
        ))}
      </div>

      <p className="text-xs text-gray-400 text-right">
        Total: {stats.total.toLocaleString('pt-BR')} votações
      </p>
    </div>
  );
}

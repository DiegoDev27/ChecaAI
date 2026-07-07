'use client';

import { useQuery } from '@tanstack/react-query';
import { alertsApi } from '@checaai/api-client';
import Link from 'next/link';
import { alertLevelColor, formatDate } from '@/lib/utils';
import { Radio, AlertTriangle } from 'lucide-react';
import { cn } from '@/lib/utils';

function LevelIcon({ level }: { level: string }) {
  const l = level.toUpperCase();
  if (l === 'CRÍTICO' || l === 'CRITICO') return <AlertTriangle className="h-5 w-5 flex-shrink-0 mt-0.5 text-red-500" />;
  if (l === 'ATENÇÃO' || l === 'ATENCAO') return <AlertTriangle className="h-5 w-5 flex-shrink-0 mt-0.5 text-orange-500" />;
  return <Radio className="h-5 w-5 flex-shrink-0 mt-0.5 text-green-500" />;
}

export function AlertsFeed() {
  const { data: alerts, isLoading, isError } = useQuery({
    queryKey: ['alerts'],
    queryFn: () => alertsApi.list(50),
    refetchInterval: 60 * 1000, // refresh every minute
  });

  if (isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="rounded-xl border p-4 animate-pulse bg-white">
            <div className="flex items-start gap-3">
              <div className="w-5 h-5 bg-slate-200 rounded flex-shrink-0 mt-0.5" />
              <div className="flex-1 space-y-2">
                <div className="h-3 bg-slate-200 rounded w-1/3" />
                <div className="h-4 bg-slate-200 rounded w-full" />
                <div className="h-4 bg-slate-200 rounded w-4/5" />
                <div className="h-3 bg-slate-100 rounded w-1/4 mt-2" />
              </div>
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="text-center py-12 text-red-600">
        Erro ao carregar votações.
      </div>
    );
  }

  if (!alerts || alerts.length === 0) {
    return (
      <div className="text-center py-16 text-slate-500">
        <Radio className="h-12 w-12 mx-auto mb-4 text-slate-300" />
        <p className="font-medium">Nenhuma votação em andamento.</p>
        <p className="text-sm mt-2">O sistema verifica a Câmara e o Senado a cada 90 segundos.</p>
        <p className="text-xs mt-1 text-slate-400">Fora do período de sessões não há votações ativas.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {alerts.map((alert) => (
        <Link
          key={alert.id}
          href={`/votacoes/${alert.votingSessionId}`}
          className={cn(
            'block rounded-xl border p-4 hover:shadow-md transition-all',
            alertLevelColor(alert.alertLevel),
          )}
        >
          <div className="flex items-start gap-3">
            <LevelIcon level={alert.alertLevel} />
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <span className="text-xs font-bold uppercase tracking-wide">
                  {alert.alertLevel}
                </span>
                {alert.score > 0 && (
                  <span className="text-xs opacity-70">Score: {alert.score}</span>
                )}
                <span className="text-xs opacity-70">•</span>
                <span className="text-xs opacity-70">{alert.chamber}</span>
              </div>
              <p className="font-medium leading-snug line-clamp-2">{alert.description}</p>
              {alert.summaryText && (
                <p className="text-sm mt-2 opacity-80 line-clamp-3">{alert.summaryText}</p>
              )}
              <p className="text-xs mt-2 opacity-60">
                Detectado: {formatDate(alert.detectedAt)}
              </p>
            </div>
          </div>
        </Link>
      ))}
    </div>
  );
}

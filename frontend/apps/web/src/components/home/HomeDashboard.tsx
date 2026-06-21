'use client';

import { useQuery } from '@tanstack/react-query';
import { politiciansApi, alertsApi, sessionsApi } from '@checa-ai/api-client';
import Link from 'next/link';
import { alertLevelColor, formatDate, voteColor, voteLabel, cn } from '@/lib/utils';
import {
  AlertTriangle, Vote, Users, Loader2, ChevronRight, ExternalLink,
} from 'lucide-react';

// ── Live Stats ────────────────────────────────────────────────────────────────

function LiveStats() {
  const { data: politicians } = useQuery({
    queryKey: ['home-politicians-count'],
    queryFn: () => politiciansApi.list({ pageSize: 1 }),
    staleTime: 5 * 60 * 1000,
  });

  const { data: sessions } = useQuery({
    queryKey: ['home-sessions-count'],
    queryFn: () => sessionsApi.list({ pageSize: 1 }),
    staleTime: 5 * 60 * 1000,
  });

  const stats = [
    {
      label: 'Parlamentares rastreados',
      value: politicians?.totalCount?.toLocaleString('pt-BR') ?? '—',
      loading: !politicians,
    },
    {
      label: 'Municípios cobertos',
      value: '5.570',
      loading: false,
    },
    {
      label: 'Votações indexadas',
      value: sessions?.totalCount?.toLocaleString('pt-BR') ?? '—',
      loading: !sessions,
    },
    {
      label: 'Dados atualizados',
      value: 'Automaticamente',
      loading: false,
    },
  ];

  return (
    <section className="bg-white border-b py-10">
      <div className="container mx-auto px-4">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-6 text-center">
          {stats.map((s) => (
            <div key={s.label}>
              <div className="text-2xl md:text-3xl font-bold text-brand-700">
                {s.loading ? (
                  <span className="inline-block w-16 h-8 bg-gray-100 animate-pulse rounded" />
                ) : (
                  s.value
                )}
              </div>
              <div className="text-sm text-gray-500 mt-1">{s.label}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

// ── Recent Alerts ─────────────────────────────────────────────────────────────

function RecentAlerts() {
  const { data: alerts, isLoading } = useQuery({
    queryKey: ['home-alerts'],
    queryFn: () => alertsApi.list(4),
    refetchInterval: 90 * 1000,
  });

  if (isLoading) {
    return (
      <div className="flex justify-center py-8">
        <Loader2 className="h-6 w-6 animate-spin text-brand-600" />
      </div>
    );
  }

  if (!alerts || alerts.length === 0) {
    return (
      <div className="text-center py-8 text-gray-400 text-sm">
        Nenhum alerta no momento. O sistema monitora votações a cada 90 segundos.
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
            <AlertTriangle className="h-4 w-4 flex-shrink-0 mt-0.5" />
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <span className="text-xs font-bold uppercase tracking-wide">
                  {alert.alertLevel}
                </span>
                <span className="text-xs opacity-70">Score: {alert.score}</span>
                <span className="text-xs opacity-70">• {alert.chamber}</span>
              </div>
              <p className="text-sm font-medium line-clamp-2 leading-snug">{alert.description}</p>
              <p className="text-xs mt-1 opacity-60">{formatDate(alert.detectedAt)}</p>
            </div>
            <ExternalLink className="h-3.5 w-3.5 flex-shrink-0 opacity-50" />
          </div>
        </Link>
      ))}
    </div>
  );
}

// ── Recent Sessions ───────────────────────────────────────────────────────────

function RecentSessions() {
  const { data, isLoading } = useQuery({
    queryKey: ['home-sessions'],
    queryFn: () => sessionsApi.list({ pageSize: 5 }),
    staleTime: 5 * 60 * 1000,
  });

  if (isLoading) {
    return (
      <div className="flex justify-center py-8">
        <Loader2 className="h-6 w-6 animate-spin text-brand-600" />
      </div>
    );
  }

  if (!data || data.data.length === 0) {
    return <div className="text-center py-8 text-gray-400 text-sm">Nenhuma votação disponível.</div>;
  }

  return (
    <div className="space-y-1.5">
      {data.data.map((s) => {
        const approved = s.result?.toLowerCase().includes('aprovad');
        return (
          <Link
            key={s.id}
            href={`/votacoes/${s.id}`}
            className="flex items-center gap-3 p-3 rounded-lg hover:bg-gray-50 transition-colors group"
          >
            <span className={cn(
              'text-xs font-bold px-2 py-0.5 rounded flex-shrink-0',
              approved ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700',
            )}>
              {approved ? '✓' : '✗'}
            </span>
            <div className="flex-1 min-w-0">
              <div className="text-sm text-gray-800 truncate group-hover:text-brand-700">
                {s.description}
              </div>
              <div className="text-xs text-gray-400 mt-0.5">
                {s.chamber} • {formatDate(s.votingDate)}
              </div>
            </div>
            <ChevronRight className="h-4 w-4 text-gray-300 flex-shrink-0 group-hover:text-brand-500" />
          </Link>
        );
      })}
    </div>
  );
}

// ── Main export ───────────────────────────────────────────────────────────────

export function HomeDashboard() {
  return (
    <>
      <LiveStats />

      <section className="py-12 px-4">
        <div className="container mx-auto max-w-5xl">
          <div className="grid lg:grid-cols-2 gap-8">
            {/* Alertas recentes */}
            <div>
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <AlertTriangle className="h-5 w-5 text-orange-500" />
                  Alertas recentes
                </h2>
                <Link href="/alertas" className="text-sm text-civic-600 hover:underline">
                  Ver todos →
                </Link>
              </div>
              <RecentAlerts />
            </div>

            {/* Últimas votações */}
            <div>
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <Vote className="h-5 w-5 text-brand-600" />
                  Últimas votações
                </h2>
                <Link href="/votacoes" className="text-sm text-civic-600 hover:underline">
                  Ver todas →
                </Link>
              </div>
              <div className="bg-white rounded-xl border overflow-hidden">
                <div className="p-1">
                  <RecentSessions />
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}

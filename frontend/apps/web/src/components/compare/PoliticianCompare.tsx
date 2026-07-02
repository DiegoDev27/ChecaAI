'use client';

import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checaai/api-client';
import type { PoliticianListItem } from '@checaai/types';
import Image from 'next/image';
import Link from 'next/link';
import { useCompletion } from 'ai/react';
import { positionLabel, presenceBadgeColor, cn } from '@/lib/utils';
import {
  User, Bot, X, Loader2, BarChart3, Vote, ArrowLeftRight,
} from 'lucide-react';
import { PoliticianPicker } from './PoliticianPicker';
import { CompareStats } from './CompareStats';
import { CommonVotes } from './CommonVotes';

type Tab = 'stats' | 'votes' | 'ai';

export function PoliticianCompare() {
  const [p1, setP1] = useState<PoliticianListItem | null>(null);
  const [p2, setP2] = useState<PoliticianListItem | null>(null);
  const [tab, setTab] = useState<Tab>('stats');
  const [agreeInfo, setAgreeInfo] = useState<{ pct: number; count: number } | null>(null);
  const [aiOpen, setAiOpen] = useState(false);

  const { data: detail1, isLoading: l1 } = useQuery({
    queryKey: ['politician', p1?.id],
    queryFn: () => politiciansApi.get(p1!.id),
    enabled: !!p1,
  });

  const { data: detail2, isLoading: l2 } = useQuery({
    queryKey: ['politician', p2?.id],
    queryFn: () => politiciansApi.get(p2!.id),
    enabled: !!p2,
  });

  const { completion, complete, isLoading: aiLoading } = useCompletion({
    api: '/api/ai/compare',
    streamProtocol: 'data',
  });

  const bothSelected = !!p1 && !!p2;
  const bothLoaded = !!detail1 && !!detail2;

  const TABS = [
    { key: 'stats' as Tab, label: 'Estatísticas', icon: <BarChart3 className="h-4 w-4" /> },
    { key: 'votes' as Tab, label: 'Votos em comum', icon: <Vote className="h-4 w-4" /> },
    { key: 'ai' as Tab, label: 'Análise IA', icon: <Bot className="h-4 w-4" /> },
  ];

  const handleAiOpen = () => {
    setTab('ai');
    setAiOpen(true);
    if (!completion) {
      complete('', {
        body: {
          id1: p1?.id,
          id2: p2?.id,
          agreePercent: agreeInfo?.pct ?? 0,
          commonCount: agreeInfo?.count ?? 0,
        },
      });
    }
  };

  return (
    <div className="space-y-6">
      {/* Pickers */}
      <div className="grid md:grid-cols-2 gap-4 items-start">
        <PoliticianPicker
          label="Parlamentar 1"
          selected={p1}
          onSelect={setP1}
          excludeId={p2?.id}
        />
        <div className="hidden md:flex items-center justify-center">
          <div className="bg-gray-100 rounded-full p-2">
            <ArrowLeftRight className="h-5 w-5 text-gray-400" />
          </div>
        </div>
        <PoliticianPicker
          label="Parlamentar 2"
          selected={p2}
          onSelect={setP2}
          excludeId={p1?.id}
        />
      </div>

      {/* Placeholder when nothing selected */}
      {!bothSelected && (
        <div className="bg-white rounded-xl border border-dashed p-12 text-center">
          <ArrowLeftRight className="h-12 w-12 mx-auto mb-4 text-gray-200" />
          <h3 className="text-gray-700 font-semibold mb-2">Compare dois parlamentares</h3>
          <p className="text-gray-400 text-sm">
            Selecione dois parlamentares acima para ver votos em comum, diferenças e análise IA.
          </p>
        </div>
      )}

      {/* Loading */}
      {bothSelected && (l1 || l2) && (
        <div className="flex justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-brand-600" />
        </div>
      )}

      {/* Comparison content */}
      {bothLoaded && detail1 && detail2 && (
        <>
          {/* Profile headers side by side */}
          <div className="grid md:grid-cols-2 gap-4">
            {[
              { data: detail1, color: 'border-brand-300 bg-brand-50', labelColor: 'text-brand-700' },
              { data: detail2, color: 'border-civic-300 bg-civic-50', labelColor: 'text-civic-700' },
            ].map(({ data, color, labelColor }, i) => (
              <Link
                key={data.id}
                href={`/parlamentares/${data.id}`}
                className={cn(
                  'flex items-center gap-4 rounded-xl border-2 p-4 hover:shadow-md transition-all',
                  color,
                )}
              >
                <div className="relative w-14 h-14 rounded-full overflow-hidden bg-white flex-shrink-0 border-2 border-white shadow">
                  {data.photoUrl ? (
                    <Image src={data.photoUrl} alt={data.fullName} fill className="object-cover object-top" sizes="56px" />
                  ) : (
                    <div className="w-full h-full flex items-center justify-center">
                      <User className="h-8 w-8 text-gray-300" />
                    </div>
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <div className={cn('font-bold text-sm leading-tight truncate', labelColor)}>
                    {data.fullName}
                  </div>
                  <div className="text-xs text-gray-600 mt-0.5">
                    {positionLabel(data.politicalPosition)} • {data.party} • {data.state}
                  </div>
                  {data.voteStats && (
                    <span className={cn(
                      'inline-block mt-1 text-xs px-2 py-0.5 rounded-full font-medium',
                      presenceBadgeColor(data.voteStats.presenceRate),
                    )}>
                      {data.voteStats.presenceRate.toFixed(1)}% presença
                    </span>
                  )}
                </div>
              </Link>
            ))}
          </div>

          {/* Tabs */}
          <div className="bg-white rounded-xl border overflow-hidden">
            <div className="flex border-b">
              {TABS.map((t) => (
                <button
                  key={t.key}
                  onClick={() => t.key === 'ai' ? handleAiOpen() : setTab(t.key)}
                  className={cn(
                    'flex items-center gap-2 px-5 py-3 text-sm font-medium border-b-2 transition-colors',
                    tab === t.key
                      ? 'border-brand-600 text-brand-700 bg-brand-50'
                      : 'border-transparent text-gray-500 hover:text-gray-800',
                  )}
                >
                  {t.icon}
                  {t.label}
                </button>
              ))}
            </div>

            <div className="p-5">
              {tab === 'stats' && <CompareStats p1={detail1} p2={detail2} />}

              {tab === 'votes' && (
                <CommonVotes
                  id1={detail1.id}
                  id2={detail2.id}
                  name1={detail1.fullName}
                  name2={detail2.fullName}
                />
              )}

              {tab === 'ai' && (
                <div className="space-y-4">
                  {aiLoading && !completion && (
                    <div className="flex items-center gap-3 text-gray-500 py-4">
                      <Loader2 className="h-5 w-5 animate-spin" />
                      Gerando análise comparativa...
                    </div>
                  )}
                  {!completion && !aiLoading && (
                    <div className="text-center py-8">
                      <Bot className="h-12 w-12 mx-auto mb-3 text-gray-200" />
                      <p className="text-gray-500 text-sm">
                        Clique na aba "Análise IA" para gerar um comparativo inteligente.
                      </p>
                    </div>
                  )}
                  {completion && (
                    <div className="prose prose-sm max-w-none text-gray-800 whitespace-pre-wrap leading-relaxed">
                      {completion}
                      {aiLoading && (
                        <span className="inline-block w-1 h-4 bg-civic-600 animate-pulse ml-0.5" />
                      )}
                    </div>
                  )}
                  <div className="text-xs text-gray-400 text-center pt-2 border-t">
                    Análise gerada por Claude AI com dados reais do ChecaAI
                  </div>
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

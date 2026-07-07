'use client';

import { useQuery } from '@tanstack/react-query';
import { sessionsApi } from '@checaai/api-client';
import Link from 'next/link';
import { formatDate, formatBRL, voteColor, voteLabel, alertLevelColor, isApproved, resultLabel, cn } from '@/lib/utils';
import {
  ChevronRight, Loader2, AlertTriangle, Users, BarChart3, Sparkles, X,
} from 'lucide-react';
import { VoteBreakdownChart } from './VoteBreakdownChart';
import { useState } from 'react';
import { useCompletion } from 'ai/react';

interface Props { id: number }

// Extended session detail from the API
interface SessionDetailData {
  id: number;
  externalId: string;
  description: string;
  votingDate: string;
  sessionType: string | null;
  totalVotes: number;
  votesYes: number;
  votesNo: number;
  votesAbstention: number;
  votesAbsent: number;
  result: string;
  chamber: string;
  proposal: {
    id: number;
    title: string;
    type: string;
    number: string | null;
    year: number;
    status: string;
  } | null;
  votes: {
    id: number;
    voteValue: string;
    politician: { id: number; fullName: string; party: string; state: string };
  }[];
  votesSummary: { yes: number; no: number; abstention: number; absent: number };
}

export function SessionDetail({ id }: Props) {
  const [showAi, setShowAi] = useState(false);
  const [partyFilter, setPartyFilter] = useState('');
  const [voteFilter, setVoteFilter] = useState('');
  const [tab, setTab] = useState<'individual' | 'party'>('party');

  const { data, isLoading, isError } = useQuery({
    queryKey: ['session', id],
    queryFn: () => sessionsApi.get(id) as unknown as Promise<SessionDetailData>,
  });

  const { completion, complete, isLoading: aiLoading } = useCompletion({
    api: `/api/ai/session/${id}/explain`,
    streamProtocol: 'data',
  });

  const handleAiOpen = () => {
    setShowAi(true);
    if (!completion) complete('');
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-24">
        <Loader2 className="h-10 w-10 animate-spin text-primary-600" />
      </div>
    );
  }

  if (isError || !data) {
    return <div className="text-center py-16 text-red-600">Votação não encontrada.</div>;
  }

  const s = data;
  const approved = isApproved(s.result);

  // Build party breakdown
  const partyMap: Record<string, Record<string, number>> = {};
  for (const v of s.votes) {
    const party = v.politician.party || 'Sem partido';
    if (!partyMap[party]) partyMap[party] = { Yes: 0, No: 0, Abstention: 0, Absent: 0 };
    partyMap[party][v.voteValue] = (partyMap[party][v.voteValue] ?? 0) + 1;
  }
  const partyRows = Object.entries(partyMap)
    .map(([party, counts]) => ({
      party,
      yes: counts.Yes ?? 0,
      no: counts.No ?? 0,
      abstention: counts.Abstention ?? 0,
      absent: counts.Absent ?? 0,
      total: Object.values(counts).reduce((a, b) => a + b, 0),
    }))
    .sort((a, b) => b.total - a.total);

  // Filter individual votes
  const filteredVotes = s.votes.filter((v) => {
    const partyOk = !partyFilter || (v.politician.party ?? '').toLowerCase().includes(partyFilter.toLowerCase());
    const voteOk = !voteFilter || v.voteValue === voteFilter;
    return partyOk && voteOk;
  });

  return (
    <div className="space-y-6">
      {/* Breadcrumb */}
      <nav className="text-sm text-slate-500 flex items-center gap-1">
        <Link href="/votacoes" className="hover:text-primary-600">Votações</Link>
        <ChevronRight className="h-3 w-3" />
        <span className="text-slate-800 truncate">{s.description}</span>
      </nav>

      {/* Header card */}
      <div className="bg-white rounded-xl border p-6">
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 mb-2 flex-wrap">
              <span className="text-xs font-medium text-slate-500 uppercase tracking-wide">
                {s.chamber}
              </span>
              <span className="text-xs text-slate-400">•</span>
              <span className="text-xs text-slate-500">{formatDate(s.votingDate)}</span>
              {s.sessionType && (
                <>
                  <span className="text-xs text-slate-400">•</span>
                  <span className="text-xs text-slate-500">{s.sessionType}</span>
                </>
              )}
            </div>
            <h1 className="text-xl font-bold text-slate-900 leading-snug">{s.description}</h1>

            {/* Result badge */}
            <div className="mt-3">
              <span className={cn(
                'text-sm font-bold px-3 py-1 rounded-full',
                approved ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800',
              )}>
                {approved ? '✓' : '✗'} {resultLabel(s.result)}
              </span>
            </div>
          </div>

          {/* AI explain button */}
          <button
            onClick={handleAiOpen}
            className="flex items-center gap-2 text-sm bg-primary-50 text-primary-700 border border-primary-200 px-4 py-2 rounded-lg hover:bg-primary-100 transition-colors font-medium flex-shrink-0"
          >
            <Sparkles className="h-4 w-4" />
            Explicar com IA
          </button>
        </div>

        {/* Proposal link */}
        {s.proposal && (
          <div className="mt-4 p-3 bg-slate-50 rounded-lg border text-sm">
            <span className="text-slate-500">Proposição: </span>
            <span className="font-medium text-slate-800">
              {s.proposal.type} {s.proposal.number}/{s.proposal.year} — {s.proposal.title}
            </span>
            <span className="ml-2 text-xs bg-slate-200 text-slate-600 px-2 py-0.5 rounded">
              {s.proposal.status}
            </span>
          </div>
        )}
      </div>

      {/* Vote summary donut-like stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {[
          { label: 'Sim', value: s.votesSummary.yes,        color: 'text-green-700 bg-green-50 border-green-200' },
          { label: 'Não', value: s.votesSummary.no,         color: 'text-red-700 bg-red-50 border-red-200' },
          { label: 'Abstenção', value: s.votesSummary.abstention, color: 'text-orange-700 bg-orange-50 border-orange-200' },
          { label: 'Ausente', value: s.votesSummary.absent, color: 'text-slate-700 bg-slate-50 border-slate-200' },
        ].map((item) => (
          <div key={item.label} className={cn('rounded-xl border p-4 text-center', item.color)}>
            <div className="text-3xl font-bold">{item.value}</div>
            <div className="text-sm font-medium mt-1">{item.label}</div>
          </div>
        ))}
      </div>

      {/* Tab: party breakdown vs individual votes */}
      <div className="bg-white rounded-xl border overflow-hidden">
        {/* Tabs */}
        <div className="flex border-b">
          {[
            { key: 'party',      label: `Por partido (${partyRows.length})`,         icon: <BarChart3 className="h-4 w-4" /> },
            { key: 'individual', label: `Votos individuais (${s.votes.length})`,      icon: <Users className="h-4 w-4" /> },
          ].map((t) => (
            <button
              key={t.key}
              onClick={() => setTab(t.key as typeof tab)}
              className={cn(
                'flex items-center gap-2 px-5 py-3 text-sm font-medium border-b-2 transition-colors',
                tab === t.key
                  ? 'border-primary-600 text-primary-700'
                  : 'border-transparent text-slate-500 hover:text-slate-800',
              )}
            >
              {t.icon}
              {t.label}
            </button>
          ))}
        </div>

        {/* Party breakdown tab */}
        {tab === 'party' && (
          <div className="p-5">
            <VoteBreakdownChart parties={partyRows} />
            <div className="mt-5 space-y-2 overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs text-slate-400 border-b">
                    <th className="pb-2 font-medium">Partido</th>
                    <th className="pb-2 text-green-700 font-medium">Sim</th>
                    <th className="pb-2 text-red-700 font-medium">Não</th>
                    <th className="pb-2 text-orange-700 font-medium">Abs.</th>
                    <th className="pb-2 text-slate-500 font-medium">Aus.</th>
                    <th className="pb-2 font-medium">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {partyRows.map((row) => (
                    <tr key={row.party} className="border-b last:border-0 hover:bg-slate-50">
                      <td className="py-2 font-semibold text-primary-700">{row.party}</td>
                      <td className="py-2 text-green-700">{row.yes}</td>
                      <td className="py-2 text-red-700">{row.no}</td>
                      <td className="py-2 text-orange-700">{row.abstention}</td>
                      <td className="py-2 text-slate-500">{row.absent}</td>
                      <td className="py-2 text-slate-700">{row.total}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Individual votes tab */}
        {tab === 'individual' && (
          <div className="p-5 space-y-4">
            {/* Filters */}
            <div className="flex gap-3 flex-wrap">
              <input
                type="text"
                value={partyFilter}
                onChange={(e) => setPartyFilter(e.target.value)}
                placeholder="Filtrar por partido..."
                className="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
              <select
                value={voteFilter}
                onChange={(e) => setVoteFilter(e.target.value)}
                className="border rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500"
              >
                <option value="">Todos os votos</option>
                <option value="Yes">Sim</option>
                <option value="No">Não</option>
                <option value="Abstention">Abstenção</option>
                <option value="Absent">Ausente</option>
              </select>
            </div>

            {/* Votes list */}
            <div className="max-h-96 overflow-y-auto space-y-1">
              {filteredVotes.map((v) => (
                <Link
                  key={v.id}
                  href={`/parlamentares/${v.politician.id}`}
                  className="flex items-center gap-3 p-2.5 rounded-lg hover:bg-slate-50 transition-colors group"
                >
                  <span className={cn(
                    'text-xs font-medium px-2 py-0.5 rounded-full flex-shrink-0 w-20 text-center',
                    voteColor(v.voteValue),
                  )}>
                    {voteLabel(v.voteValue)}
                  </span>
                  <div className="flex-1 min-w-0">
                    <span className="text-sm text-slate-800 group-hover:text-primary-700 truncate block">
                      {v.politician.fullName}
                    </span>
                  </div>
                  <div className="flex gap-2 text-xs text-slate-500 flex-shrink-0">
                    <span className="font-medium text-slate-700">{v.politician.party}</span>
                    <span>{v.politician.state}</span>
                  </div>
                </Link>
              ))}
              {filteredVotes.length === 0 && (
                <div className="text-center py-8 text-slate-400">Nenhum voto encontrado.</div>
              )}
            </div>
          </div>
        )}
      </div>

      {/* AI Explanation Modal */}
      {showAi && (
        <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[80vh] flex flex-col animate-fade-scale-in">
            <div className="flex items-center justify-between p-4 border-b">
              <div className="flex items-center gap-2 font-semibold">
                <Sparkles className="h-5 w-5 text-primary-600" />
                Explicação IA
              </div>
              <button onClick={() => setShowAi(false)} className="p-1 rounded hover:bg-slate-100 text-slate-500">
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-5">
              {aiLoading && !completion && (
                <div className="flex items-center gap-3 text-slate-500">
                  <Loader2 className="h-5 w-5 animate-spin" />
                  Gerando explicação...
                </div>
              )}
              {completion && (
                <div className="prose prose-sm max-w-none text-slate-800 whitespace-pre-wrap leading-relaxed">
                  {completion}
                  {aiLoading && <span className="inline-block w-1 h-4 bg-primary-600 animate-pulse ml-0.5" />}
                </div>
              )}
            </div>
            <div className="p-3 border-t text-xs text-slate-400 text-center">
              Análise gerada por Claude AI com dados reais do ChecaAI
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

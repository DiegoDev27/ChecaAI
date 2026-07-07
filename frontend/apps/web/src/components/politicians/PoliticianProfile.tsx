'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checaai/api-client';
import Image from 'next/image';
import Link from 'next/link';
import { positionLabel, presenceBadgeColor, cn } from '@/lib/utils';
import {
  User, ExternalLink, Mail, ChevronRight, Loader2,
  LayoutDashboard, Vote, Receipt, DollarSign, Gift,
  Users, Briefcase, CalendarCheck, TrendingUp, Building2,
} from 'lucide-react';
import { AiAnalysisButton } from './AiAnalysisButton';
import { OverviewTab } from './tabs/OverviewTab';
import { VotesTab } from './tabs/VotesTab';
import { ExpensesTab } from './tabs/ExpensesTab';
import { SalariesTab } from './tabs/SalariesTab';
import { AllowancesTab } from './tabs/AllowancesTab';
import { StaffTab } from './tabs/StaffTab';
import { AttendanceTab } from './tabs/AttendanceTab';
import { CommitteesTab } from './tabs/CommitteesTab';
import { CampaignTab } from './tabs/CampaignTab';

interface Props { id: number }

type Tab =
  | 'overview'
  | 'votes'
  | 'expenses'
  | 'salaries'
  | 'allowances'
  | 'staff'
  | 'attendance'
  | 'committees'
  | 'campaign';

const TABS: { key: Tab; label: string; icon: React.ReactNode }[] = [
  { key: 'overview',    label: 'Visão Geral',   icon: <LayoutDashboard className="h-4 w-4" /> },
  { key: 'votes',       label: 'Votos',          icon: <Vote className="h-4 w-4" /> },
  { key: 'expenses',    label: 'Despesas',        icon: <Receipt className="h-4 w-4" /> },
  { key: 'salaries',    label: 'Salários',        icon: <DollarSign className="h-4 w-4" /> },
  { key: 'allowances',  label: 'Auxílios',        icon: <Gift className="h-4 w-4" /> },
  { key: 'staff',       label: 'Assessores',      icon: <Users className="h-4 w-4" /> },
  { key: 'attendance',  label: 'Presença',        icon: <CalendarCheck className="h-4 w-4" /> },
  { key: 'committees',  label: 'Comissões',       icon: <Building2 className="h-4 w-4" /> },
  { key: 'campaign',    label: 'Campanha',         icon: <TrendingUp className="h-4 w-4" /> },
];

export function PoliticianProfile({ id }: Props) {
  const [tab, setTab] = useState<Tab>('overview');
  const [imgError, setImgError] = useState(false);

  const { data: p, isLoading, isError } = useQuery({
    queryKey: ['politician', id],
    queryFn: () => politiciansApi.get(id),
    staleTime: 5 * 60 * 1000,
  });

  if (isLoading) {
    return (
      <div className="flex justify-center py-24">
        <Loader2 className="h-10 w-10 animate-spin text-primary-600" />
      </div>
    );
  }

  if (isError || !p) {
    return (
      <div className="text-center py-16 text-red-600">
        Parlamentar não encontrado ou erro ao carregar.
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Breadcrumb */}
      <nav className="text-sm text-slate-500 flex items-center gap-1">
        <Link href="/parlamentares" className="hover:text-primary-600">Parlamentares</Link>
        <ChevronRight className="h-3 w-3" />
        <span className="text-slate-800">{p.fullName}</span>
      </nav>

      {/* Hero card — always visible */}
      <div className="bg-white rounded-xl border p-6">
        <div className="flex flex-col sm:flex-row gap-5">
          {/* Photo */}
          <div className="relative w-24 h-24 sm:w-28 sm:h-28 rounded-full overflow-hidden bg-slate-100 flex-shrink-0 self-center sm:self-start border-4 border-white shadow-md">
            {p.photoUrl && !imgError ? (
              <Image
                src={p.photoUrl}
                alt={p.fullName}
                fill
                className="object-cover object-center"
                sizes="112px"
                onError={() => setImgError(true)}
              />
            ) : (
              <div className="w-full h-full flex items-center justify-center bg-slate-100">
                <User className="h-12 w-12 text-slate-400" />
              </div>
            )}
          </div>

          {/* Info */}
          <div className="flex-1 min-w-0">
            <h1 className="text-2xl font-bold text-slate-900 leading-tight">{p.fullName}</h1>
            <p className="text-slate-600 mt-1 text-sm">
              {positionLabel(p.politicalPosition)}
              {p.state && <span> • {p.state}</span>}
              {p.party && (
                <span className="ml-1.5 inline-flex items-center gap-1">
                  •
                  <Link
                    href={`/parlamentares?party=${p.party}`}
                    className="text-primary-700 font-medium hover:underline"
                  >
                    {p.party}
                  </Link>
                </span>
              )}
            </p>

            {/* Presence badge */}
            {p.voteStats && (
              <div className="mt-2">
                <span className={cn(
                  'text-xs font-medium px-2.5 py-1 rounded-full',
                  presenceBadgeColor(p.voteStats.presenceRate),
                )}>
                  {p.voteStats.presenceRate.toFixed(1)}% em votações nominais • {p.voteStats.total} votos
                </span>
              </div>
            )}

            {/* Contact links */}
            <div className="flex flex-wrap gap-3 mt-3">
              {p.email && (
                <a
                  href={`mailto:${p.email}`}
                  className="flex items-center gap-1.5 text-xs text-primary-600 hover:underline"
                >
                  <Mail className="h-3.5 w-3.5" />
                  {p.email}
                </a>
              )}
              {p.website && (
                <a
                  href={p.website}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center gap-1.5 text-xs text-primary-600 hover:underline"
                >
                  <ExternalLink className="h-3.5 w-3.5" />
                  Site oficial
                </a>
              )}
            </div>
          </div>

          {/* AI button */}
          <div className="self-start">
            <AiAnalysisButton politicianId={id} politicianName={p.fullName} />
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="bg-white rounded-xl border overflow-hidden">
        {/* Tab bar */}
        <div className="border-b overflow-x-auto">
          <div className="flex min-w-max">
            {TABS.map((t) => (
              <button
                key={t.key}
                onClick={() => setTab(t.key)}
                className={cn(
                  'flex items-center gap-1 px-3 py-3 text-xs font-medium border-b-2 whitespace-nowrap transition-colors',
                  tab === t.key
                    ? 'border-primary-600 text-primary-700 bg-primary-50'
                    : 'border-transparent text-slate-500 hover:text-slate-800 hover:bg-slate-50',
                )}
              >
                <span className="hidden sm:block">{t.icon}</span>
                {t.label}
              </button>
            ))}
          </div>
        </div>

        {/* Tab content */}
        <div className="p-5">
          {tab === 'overview'   && <OverviewTab p={p} id={id} />}
          {tab === 'votes'      && <VotesTab id={id} voteStats={p.voteStats} />}
          {tab === 'expenses'   && <ExpensesTab id={id} expenseSummary={p.expenseSummary} />}
          {tab === 'salaries'   && <SalariesTab id={id} />}
          {tab === 'allowances' && <AllowancesTab id={id} />}
          {tab === 'staff'      && <StaffTab id={id} />}
          {tab === 'attendance'  && <AttendanceTab id={id} />}
          {tab === 'committees' && <CommitteesTab committees={p.committees ?? []} />}
          {tab === 'campaign'   && <CampaignTab id={id} />}
        </div>
      </div>
    </div>
  );
}

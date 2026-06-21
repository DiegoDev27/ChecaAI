'use client';

import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { partiesApi } from '@checa-ai/api-client';
import type { Party } from '@checa-ai/types';
import Image from 'next/image';
import Link from 'next/link';
import { Loader2, Users, Search, ArrowUpDown } from 'lucide-react';
import { cn } from '@/lib/utils';

/** Official party logo from Câmara dos Deputados CDN */
function partyLogoUrl(acronym: string) {
  return `https://www.camara.leg.br/internet/Deputado/img/partidos/${acronym}.gif`;
}

type SortKey = 'members' | 'acronym' | 'number';

export function PartiesList() {
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<SortKey>('members');

  const { data: parties, isLoading, isError } = useQuery({
    queryKey: ['parties'],
    queryFn: () => partiesApi.list(true),
    staleTime: 5 * 60 * 1000,
  });

  const filtered = useMemo(() => {
    if (!parties) return [];
    let list = [...parties];

    if (search.trim()) {
      const q = search.toLowerCase();
      list = list.filter(
        (p) =>
          p.acronym.toLowerCase().includes(q) ||
          p.fullName.toLowerCase().includes(q) ||
          (p.president ?? '').toLowerCase().includes(q),
      );
    }

    list.sort((a, b) => {
      if (sort === 'members') return b.memberCount - a.memberCount;
      if (sort === 'acronym') return a.acronym.localeCompare(b.acronym);
      if (sort === 'number') return (a.number ?? 999) - (b.number ?? 999);
      return 0;
    });

    return list;
  }, [parties, search, sort]);

  if (isLoading) {
    return (
      <div className="flex justify-center py-16">
        <Loader2 className="h-8 w-8 animate-spin text-brand-600" />
      </div>
    );
  }

  if (isError) {
    return <div className="text-center py-12 text-red-600">Erro ao carregar partidos.</div>;
  }

  return (
    <div className="space-y-5">
      {/* Controls */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
          <input
            type="text"
            placeholder="Buscar partido, sigla ou presidente…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-9 pr-4 py-2 border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-brand-500 bg-white"
          />
        </div>

        <div className="flex items-center gap-2">
          <ArrowUpDown className="h-4 w-4 text-gray-400 flex-shrink-0" />
          <select
            value={sort}
            onChange={(e) => setSort(e.target.value as SortKey)}
            className="text-sm border rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-brand-500 bg-white"
          >
            <option value="members">Mais membros</option>
            <option value="acronym">Sigla (A–Z)</option>
            <option value="number">Número eleitoral</option>
          </select>
        </div>
      </div>

      {/* Count */}
      <p className="text-sm text-gray-500">
        {filtered.length} partido{filtered.length !== 1 ? 's' : ''} encontrado{filtered.length !== 1 ? 's' : ''}
      </p>

      {/* Grid */}
      {filtered.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          Nenhum partido encontrado para "{search}".
        </div>
      ) : (
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map((party) => (
            <PartyCard key={party.id} party={party} />
          ))}
        </div>
      )}
    </div>
  );
}

function partyColorClass(acronym: string): string {
  // A simple deterministic color assignment based on acronym
  const colors = [
    'bg-red-100 text-red-700 border-red-200',
    'bg-blue-100 text-blue-700 border-blue-200',
    'bg-green-100 text-green-700 border-green-200',
    'bg-purple-100 text-purple-700 border-purple-200',
    'bg-orange-100 text-orange-700 border-orange-200',
    'bg-yellow-100 text-yellow-700 border-yellow-200',
    'bg-teal-100 text-teal-700 border-teal-200',
    'bg-pink-100 text-pink-700 border-pink-200',
    'bg-indigo-100 text-indigo-700 border-indigo-200',
    'bg-cyan-100 text-cyan-700 border-cyan-200',
  ];
  let hash = 0;
  for (const c of acronym) hash = (hash * 31 + c.charCodeAt(0)) % colors.length;
  return colors[Math.abs(hash) % colors.length];
}

function PartyCard({ party }: { party: Party }) {
  const [logoError, setLogoError] = useState(false);
  const colorCls = partyColorClass(party.acronym);

  // Shorten very long acronyms for the badge fallback
  const badgeText = party.acronym.length > 6
    ? party.acronym.slice(0, 5) + '…'
    : party.acronym;
  const badgeFontSize = party.acronym.length > 4 ? 'text-[10px]' : 'text-sm';

  return (
    <Link
      href={`/partidos/${party.acronym}`}
      className="group bg-white rounded-xl border hover:border-brand-300 hover:shadow-md transition-all p-4 flex flex-col gap-3"
    >
      {/* Header */}
      <div className="flex items-center gap-3">
        {/* Logo — official Câmara CDN, fallback to colored badge */}
        <div className="w-14 h-14 rounded-lg border flex-shrink-0 flex items-center justify-center overflow-hidden bg-white">
          {!logoError ? (
            <Image
              src={partyLogoUrl(party.acronym)}
              alt={`Logo ${party.acronym}`}
              width={56}
              height={56}
              className="object-contain"
              onError={() => setLogoError(true)}
              unoptimized
            />
          ) : (
            <div className={cn(
              'w-full h-full rounded-lg border-2 flex items-center justify-center font-bold overflow-hidden',
              badgeFontSize,
              colorCls,
            )}>
              {badgeText}
            </div>
          )}
        </div>

        <div className="min-w-0 flex-1">
          <div className="font-semibold text-gray-900 text-sm leading-tight truncate group-hover:text-brand-700 transition-colors">
            {party.fullName}
          </div>
          <div className="flex items-center gap-2 mt-1 flex-wrap">
            <span className="text-xs font-bold text-gray-500">{party.acronym}</span>
            {party.number && (
              <span className="text-xs text-gray-400 font-mono">Nº {party.number}</span>
            )}
          </div>
        </div>
      </div>

      {/* President */}
      {party.president && (
        <div className="text-xs text-gray-500 leading-snug">
          <span className="text-gray-400">Presidente:</span>{' '}
          <span className="text-gray-700">{party.president}</span>
        </div>
      )}

      {/* Member count */}
      <div className="flex items-center gap-1.5 mt-auto">
        <Users className="h-3.5 w-3.5 text-brand-500" />
        <span className="text-sm font-medium text-brand-700">
          {party.memberCount.toLocaleString('pt-BR')}
        </span>
        <span className="text-sm text-gray-500">membros</span>
      </div>
    </Link>
  );
}

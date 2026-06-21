'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { partiesApi } from '@checa-ai/api-client';
import type { PoliticianListItem } from '@checa-ai/types';
import Image from 'next/image';
import Link from 'next/link';
import {
  Loader2, Users, User, ArrowLeft, ChevronLeft, ChevronRight,
} from 'lucide-react';

function partyLogoUrl(acronym: string) {
  return `https://www.camara.leg.br/internet/Deputado/img/partidos/${acronym}.gif`;
}
import { positionLabel, cn } from '@/lib/utils';

const POSITIONS = [
  { value: '', label: 'Todos os cargos' },
  { value: 'Senator', label: 'Senador' },
  { value: 'Federal Deputy', label: 'Deputado Federal' },
  { value: 'State Deputy', label: 'Deputado Estadual' },
  { value: 'Governor', label: 'Governador' },
  { value: 'Mayor', label: 'Prefeito' },
  { value: 'City Councilor', label: 'Vereador' },
];

interface Props {
  acronym: string;
}

export function PartyDetail({ acronym }: Props) {
  const [position, setPosition] = useState('');
  const [page, setPage] = useState(1);
  const [logoError, setLogoError] = useState(false);

  const { data: party, isLoading: partyLoading, isError: partyError } = useQuery({
    queryKey: ['party', acronym],
    queryFn: () => partiesApi.get(acronym),
    staleTime: 5 * 60 * 1000,
  });

  const { data: members, isLoading: membersLoading } = useQuery({
    queryKey: ['party-members', acronym, position, page],
    queryFn: () => partiesApi.members(acronym, position || undefined, page, 24),
    staleTime: 3 * 60 * 1000,
    enabled: !!party,
  });

  if (partyLoading) {
    return (
      <div className="flex justify-center py-16">
        <Loader2 className="h-8 w-8 animate-spin text-brand-600" />
      </div>
    );
  }

  if (partyError || !party) {
    return (
      <div className="text-center py-12 space-y-4">
        <p className="text-red-600">Partido não encontrado.</p>
        <Link href="/partidos" className="text-brand-600 hover:underline text-sm">
          ← Voltar para partidos
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Back */}
      <Link
        href="/partidos"
        className="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-brand-700 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" />
        Partidos
      </Link>

      {/* Party header */}
      <div className="bg-white rounded-2xl border shadow-sm p-6">
        <div className="flex flex-col sm:flex-row items-start gap-5">
          {/* Logo — official Câmara CDN, fallback to acronym badge */}
          <div className="w-20 h-20 rounded-xl border-2 border-gray-100 flex items-center justify-center flex-shrink-0 bg-white overflow-hidden">
            {!logoError ? (
              <Image
                src={partyLogoUrl(party.acronym)}
                alt={`Logo ${party.acronym}`}
                width={80}
                height={80}
                className="object-contain p-1"
                onError={() => setLogoError(true)}
                unoptimized
              />
            ) : (
              <div className="w-full h-full bg-brand-100 border-2 border-brand-200 rounded-xl flex items-center justify-center">
                <span className="text-xl font-bold text-brand-700 px-1 text-center leading-tight">
                  {party.acronym}
                </span>
              </div>
            )}
          </div>

          <div className="flex-1 min-w-0">
            <h1 className="text-2xl font-bold text-gray-900 leading-tight">{party.fullName}</h1>
            <div className="flex flex-wrap items-center gap-3 mt-2">
              {party.number && (
                <span className="text-sm bg-gray-100 text-gray-600 px-2.5 py-1 rounded-full font-mono">
                  Nº {party.number}
                </span>
              )}
              <span className={cn(
                'text-xs px-2.5 py-1 rounded-full font-medium',
                party.isActive
                  ? 'bg-green-100 text-green-700'
                  : 'bg-gray-100 text-gray-500',
              )}>
                {party.isActive ? 'Ativo' : 'Inativo'}
              </span>
            </div>
          </div>

          {/* Member count */}
          <div className="text-center sm:text-right flex-shrink-0">
            <div className="text-4xl font-bold text-brand-700">
              {party.memberCount.toLocaleString('pt-BR')}
            </div>
            <div className="text-sm text-gray-500 mt-0.5 flex items-center gap-1 justify-center sm:justify-end">
              <Users className="h-3.5 w-3.5" />
              membros
            </div>
          </div>
        </div>

        {/* President */}
        {party.president && (
          <div className="mt-4 pt-4 border-t">
            <span className="text-sm text-gray-500">Presidente do partido:</span>{' '}
            <span className="text-sm font-semibold text-gray-800">{party.president}</span>
          </div>
        )}
      </div>

      {/* Members section */}
      <div className="space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <h2 className="text-lg font-semibold text-gray-800 flex items-center gap-2">
            <Users className="h-5 w-5 text-brand-600" />
            Membros do {party.acronym}
          </h2>
          <select
            value={position}
            onChange={(e) => { setPosition(e.target.value); setPage(1); }}
            className="text-sm border rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-brand-500 bg-white"
          >
            {POSITIONS.map((p) => (
              <option key={p.value} value={p.value}>{p.label}</option>
            ))}
          </select>
        </div>

        {membersLoading ? (
          <div className="flex justify-center py-10">
            <Loader2 className="h-6 w-6 animate-spin text-brand-600" />
          </div>
        ) : !members || members.data.length === 0 ? (
          <div className="text-center py-10 text-gray-400">
            Nenhum membro encontrado para esse filtro.
          </div>
        ) : (
          <>
            <div className="grid sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
              {members.data.map((politician) => (
                <MemberCard key={politician.id} politician={politician} />
              ))}
            </div>

            {/* Pagination */}
            {members.totalPages > 1 && (
              <div className="flex items-center justify-between pt-2">
                <span className="text-sm text-gray-500">
                  Página {members.page} de {members.totalPages} ({members.totalCount.toLocaleString('pt-BR')} membros)
                </span>
                <div className="flex gap-2">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={!members.hasPrevPage}
                    className="p-2 rounded-lg border text-sm font-medium disabled:opacity-40 hover:bg-gray-50 transition-colors"
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => setPage((p) => p + 1)}
                    disabled={!members.hasNextPage}
                    className="p-2 rounded-lg border text-sm font-medium disabled:opacity-40 hover:bg-gray-50 transition-colors"
                  >
                    <ChevronRight className="h-4 w-4" />
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

function MemberCard({ politician }: { politician: PoliticianListItem }) {
  const [imgError, setImgError] = useState(false);
  const showPhoto = !!politician.photoUrl && !imgError;

  return (
    <Link
      href={`/parlamentares/${politician.id}`}
      className="flex items-center gap-3 bg-white rounded-xl border hover:border-brand-300 hover:shadow-sm transition-all p-3 group"
    >
      <div className="relative w-10 h-10 rounded-full overflow-hidden bg-gray-100 flex-shrink-0 border border-gray-200">
        {showPhoto ? (
          <Image
            src={politician.photoUrl!}
            alt={politician.fullName}
            fill
            className="object-cover object-center"
            sizes="40px"
            onError={() => setImgError(true)}
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center">
            <User className="h-5 w-5 text-gray-300" />
          </div>
        )}
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-sm font-medium text-gray-800 truncate group-hover:text-brand-700 transition-colors leading-tight">
          {politician.fullName}
        </div>
        <div className="text-xs text-gray-400 mt-0.5 truncate">
          {positionLabel(politician.politicalPosition)}
          {politician.state && ` • ${politician.state}`}
        </div>
      </div>
    </Link>
  );
}

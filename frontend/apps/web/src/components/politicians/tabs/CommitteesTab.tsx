'use client';

import type { CommitteeMembership } from '@checaai/types';
import { Briefcase } from 'lucide-react';

interface Props {
  committees: CommitteeMembership[];
}

const ROLE_COLOR: Record<string, string> = {
  Presidente:      'bg-brand-100 text-brand-800',
  'Vice-Presidente': 'bg-civic-100 text-civic-800',
  Titular:         'bg-green-100 text-green-800',
  Suplente:        'bg-yellow-100 text-yellow-800',
};

function roleColor(role: string) {
  return ROLE_COLOR[role] ?? 'bg-gray-100 text-gray-700';
}

const TYPE_LABEL: Record<string, string> = {
  Permanente: 'Permanente',
  Temporária: 'Temporária',
  CPI:        'CPI',
  Especial:   'Especial',
  Mista:      'Mista',
};

export function CommitteesTab({ committees }: Props) {
  if (committees.length === 0) {
    return (
      <div className="bg-white rounded-xl border p-10 text-center">
        <Briefcase className="h-10 w-10 mx-auto mb-3 text-gray-200" />
        <p className="text-gray-500 font-medium text-sm mb-1">Nenhuma comissão registrada</p>
        <p className="text-gray-400 text-sm">
          Os dados de comissões são sincronizados a partir da API da Câmara dos Deputados e do Senado Federal.
        </p>
      </div>
    );
  }

  // Group by committeeType
  const grouped = committees.reduce<Record<string, CommitteeMembership[]>>((acc, c) => {
    const key = TYPE_LABEL[c.committeeType] ?? c.committeeType ?? 'Outras';
    if (!acc[key]) acc[key] = [];
    acc[key].push(c);
    return acc;
  }, {});

  const typeOrder = ['Permanente', 'Mista', 'CPI', 'Especial', 'Temporária', 'Outras'];
  const sortedGroups = Object.entries(grouped).sort(
    ([a], [b]) => typeOrder.indexOf(a) - typeOrder.indexOf(b),
  );

  return (
    <div className="space-y-5">
      {/* Summary bar */}
      <div className="bg-white rounded-xl border p-4 flex items-center gap-6 flex-wrap">
        <div className="flex items-center gap-2">
          <Briefcase className="h-5 w-5 text-brand-600" />
          <span className="font-semibold text-gray-800">
            {committees.length} comissão{committees.length !== 1 ? 'ões' : ''}
          </span>
        </div>
        {Object.entries(grouped).map(([type, items]) => (
          <div key={type} className="text-sm text-gray-500">
            <span className="font-medium text-gray-800">{items.length}</span> {type.toLowerCase()}
          </div>
        ))}
      </div>

      {/* Groups */}
      {sortedGroups.map(([type, items]) => (
        <div key={type} className="bg-white rounded-xl border overflow-hidden">
          <div className="px-5 py-3 bg-gray-50 border-b">
            <h3 className="text-sm font-semibold text-gray-700">{type}</h3>
          </div>
          <div className="divide-y">
            {items.map((c) => (
              <div key={c.committeeId} className="flex items-center gap-4 px-5 py-3.5">
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-medium text-gray-800 leading-snug">
                    {c.committeeName}
                    {c.acronym && (
                      <span className="ml-1.5 text-xs text-gray-400">({c.acronym})</span>
                    )}
                  </div>
                  <div className="text-xs text-gray-400 mt-0.5">{c.chamber}</div>
                </div>
                <span className={`text-xs font-medium px-2.5 py-0.5 rounded-full flex-shrink-0 ${roleColor(c.role)}`}>
                  {c.role}
                </span>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

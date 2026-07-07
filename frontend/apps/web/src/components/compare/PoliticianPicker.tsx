'use client';

import { useState, useRef, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { politiciansApi } from '@checaai/api-client';
import type { PoliticianListItem } from '@checaai/types';
import Image from 'next/image';
import { Search, User, X, Loader2 } from 'lucide-react';
import { positionLabel } from '@/lib/utils';
import { useDebounce } from '@/lib/useDebounce';

interface Props {
  label: string;
  selected: PoliticianListItem | null;
  onSelect: (p: PoliticianListItem | null) => void;
  excludeId?: number;
}

export function PoliticianPicker({ label, selected, onSelect, excludeId }: Props) {
  const [q, setQ] = useState('');
  const [open, setOpen] = useState(false);
  const debouncedQ = useDebounce(q, 350);
  const containerRef = useRef<HTMLDivElement>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['picker-search', debouncedQ],
    queryFn: () => politiciansApi.list({ q: debouncedQ || undefined, pageSize: 8 }),
    enabled: open && debouncedQ.length > 1,
  });

  // Close dropdown when clicking outside
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const results = (data?.data ?? []).filter((p) => p.id !== excludeId);

  if (selected) {
    return (
      <div className="bg-white rounded-xl border-2 border-primary-200 p-4 flex items-center gap-4">
        <div className="relative w-14 h-14 rounded-full overflow-hidden bg-slate-100 flex-shrink-0">
          {selected.photoUrl ? (
            <Image src={selected.photoUrl} alt={selected.fullName} fill className="object-cover object-top" sizes="56px" />
          ) : (
            <div className="w-full h-full flex items-center justify-center">
              <User className="h-8 w-8 text-slate-300" />
            </div>
          )}
        </div>
        <div className="flex-1 min-w-0">
          <div className="font-semibold text-slate-900 truncate">{selected.fullName}</div>
          <div className="text-sm text-slate-500 mt-0.5">
            {positionLabel(selected.politicalPosition)}
            {selected.party && ` • ${selected.party}`}
            {selected.state && ` • ${selected.state}`}
          </div>
        </div>
        <button
          onClick={() => onSelect(null)}
          className="p-1.5 rounded-lg hover:bg-slate-100 text-slate-400 hover:text-slate-600 flex-shrink-0"
          title="Remover"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="bg-white rounded-xl border-2 border-dashed border-slate-200 p-4 hover:border-primary-300 transition-colors">
        <div className="text-xs font-medium text-slate-500 mb-2 uppercase tracking-wide">{label}</div>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
          <input
            type="text"
            value={q}
            onChange={(e) => { setQ(e.target.value); setOpen(true); }}
            onFocus={() => setOpen(true)}
            placeholder="Buscar parlamentar..."
            className="w-full pl-9 pr-4 py-2.5 border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 bg-slate-50"
          />
          {isLoading && (
            <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 animate-spin" />
          )}
        </div>
      </div>

      {/* Dropdown */}
      {open && results.length > 0 && (
        <div className="absolute z-20 mt-1 w-full bg-white rounded-xl border shadow-lg overflow-hidden">
          {results.map((p) => (
            <button
              key={p.id}
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => { onSelect(p); setQ(''); setOpen(false); }}
              className="w-full flex items-center gap-3 px-4 py-3 hover:bg-slate-50 transition-colors text-left"
            >
              <div className="relative w-8 h-8 rounded-full overflow-hidden bg-slate-100 flex-shrink-0">
                {p.photoUrl ? (
                  <Image src={p.photoUrl} alt={p.fullName} fill className="object-cover object-top" sizes="32px" />
                ) : (
                  <div className="w-full h-full flex items-center justify-center">
                    <User className="h-4 w-4 text-slate-300" />
                  </div>
                )}
              </div>
              <div className="flex-1 min-w-0">
                <div className="text-sm font-medium text-slate-900 truncate">{p.fullName}</div>
                <div className="text-xs text-slate-500">
                  {positionLabel(p.politicalPosition)}{p.party && ` • ${p.party}`}{p.state && ` • ${p.state}`}
                </div>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

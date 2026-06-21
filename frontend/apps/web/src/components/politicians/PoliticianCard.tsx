'use client';

import Link from 'next/link';
import Image from 'next/image';
import { useState } from 'react';
import type { PoliticianListItem } from '@checa-ai/types';
import { positionLabel } from '@/lib/utils';

interface Props {
  politician: PoliticianListItem;
}

export function PoliticianCard({ politician: p }: Props) {
  const [imgError, setImgError] = useState(false);
  const showPhoto = !!p.photoUrl && !imgError;

  return (
    <Link
      href={`/parlamentares/${p.id}`}
      className="group bg-white rounded-xl border hover:border-brand-300 hover:shadow-md transition-all flex flex-col overflow-hidden"
    >
      {/* Photo */}
      <div className="relative h-48 bg-gray-50 flex items-center justify-center overflow-hidden">
        {showPhoto ? (
          <Image
            src={p.photoUrl!}
            alt={p.fullName}
            fill
            className="object-contain object-top"
            sizes="(max-width: 640px) 50vw, (max-width: 1024px) 33vw, 25vw"
            onError={() => setImgError(true)}
          />
        ) : (
          <div className="flex flex-col items-center justify-center w-full h-full bg-gradient-to-br from-gray-50 to-gray-100">
            <div className="w-20 h-20 rounded-full bg-brand-100 border-2 border-brand-200 flex items-center justify-center shadow-sm">
              <span className="text-2xl font-bold text-brand-700 select-none">
                {p.fullName.split(' ').filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join('')}
              </span>
            </div>
          </div>
        )}
      </div>

      {/* Info */}
      <div className="p-4 flex-1 flex flex-col gap-1">
        <h3 className="font-semibold text-gray-900 text-sm leading-tight group-hover:text-brand-700 transition-colors line-clamp-2">
          {p.fullName}
        </h3>
        <p className="text-xs text-gray-500">{positionLabel(p.politicalPosition)}</p>

        <div className="flex items-center gap-2 mt-auto pt-2">
          {p.party && (
            <span className="text-xs bg-gray-100 text-gray-700 px-2 py-0.5 rounded font-medium">
              {p.party}
            </span>
          )}
          {p.state && (
            <span className="text-xs text-gray-500">{p.state}</span>
          )}
        </div>
      </div>
    </Link>
  );
}

import type { Metadata } from 'next';
import { PartyDetail } from '@/components/parties/PartyDetail';
import { Flag } from 'lucide-react';

interface Props {
  params: { acronym: string };
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return {
    title: `Partido ${params.acronym} | Checa Aí`,
    description: `Membros, líderes e informações do ${params.acronym}.`,
  };
}

export default function PartyPage({ params }: Props) {
  return (
    <div className="container mx-auto px-4 py-8 max-w-5xl">
      <PartyDetail acronym={params.acronym} />
    </div>
  );
}

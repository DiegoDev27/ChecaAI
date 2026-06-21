import type { Metadata } from 'next';
import { AlertsFeed } from '@/components/common/AlertsFeed';
import { LiveBadge } from '@/components/common/LiveBadge';

export const metadata: Metadata = {
  title: 'Alertas',
  description: 'Votações suspeitas detectadas em tempo real: madrugada, urgência, quórum baixo.',
};

export default function AlertasPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-3xl">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <h1 className="text-3xl font-bold text-gray-900">⚠️ Alertas de Votação</h1>
          <LiveBadge />
        </div>
        <p className="text-gray-600">
          Votações classificadas como suspeitas — realizadas de madrugada, em regime de urgência,
          com quórum reduzido ou palavras-chave polêmicas na ementa.
        </p>
        <div className="mt-3 text-xs text-gray-400">
          O sistema verifica votações a cada 90 segundos. Alertas ATENÇÃO e CRÍTICO são notificados
          via push em tempo real via WebSocket.
        </div>
      </div>
      <AlertsFeed />
    </div>
  );
}

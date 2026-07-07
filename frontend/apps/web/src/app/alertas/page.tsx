import type { Metadata } from 'next';
import { AlertsFeed } from '@/components/common/AlertsFeed';
import { LiveBadge } from '@/components/common/LiveBadge';

export const metadata: Metadata = {
  title: 'Plenário ao Vivo',
  description: 'Todas as votações do Congresso em tempo real — acompanhe o que está sendo votado agora.',
};

export default function AlertasPage() {
  return (
    <div className="container mx-auto px-4 py-8 max-w-3xl">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <h1 className="text-3xl font-bold text-slate-900">Plenário ao Vivo</h1>
          <LiveBadge />
        </div>
        <p className="text-slate-600">
          Todas as votações em andamento na Câmara e no Senado, detectadas a cada 90 segundos.
          Votações com características suspeitas (madrugada, urgência, quórum baixo) são destacadas.
        </p>
        <div className="mt-3 text-xs text-slate-400">
          Nível <span className="font-medium text-green-600">NORMAL</span> — votação ordinária &nbsp;·&nbsp;
          <span className="font-medium text-orange-500">ATENÇÃO</span> — critérios de alerta &nbsp;·&nbsp;
          <span className="font-medium text-red-600">CRÍTICO</span> — score alto (score ≥ 60)
        </div>
      </div>
      <AlertsFeed />
    </div>
  );
}

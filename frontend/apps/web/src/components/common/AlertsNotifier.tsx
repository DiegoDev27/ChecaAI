'use client';

import { useState, useCallback, useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useSignalRAlerts, type AlertPayload } from '@/lib/useSignalRAlerts';
import { alertLevelColor, cn } from '@/lib/utils';
import { AlertTriangle, X, Wifi, WifiOff } from 'lucide-react';

interface ToastItem extends AlertPayload {
  toastId: string;
  createdAt: number;
}

const TOAST_DURATION = 8000; // ms

export function AlertsNotifier() {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const queryClient = useQueryClient();
  const router = useRouter();

  const dismiss = useCallback((toastId: string) => {
    setToasts((prev) => prev.filter((t) => t.toastId !== toastId));
  }, []);

  const handleAlert = useCallback(
    (alert: AlertPayload) => {
      // Always refresh the feed so NORMAL sessions appear too
      queryClient.invalidateQueries({ queryKey: ['alerts'] });
      queryClient.invalidateQueries({ queryKey: ['home-alerts'] });

      // Show toasts only for ATENÇÃO or CRÍTICO
      const level = alert.alertLevel.toUpperCase();
      if (level === 'NORMAL') return;

      const toastId = `alert-${alert.alertId}-${Date.now()}`;
      const toast: ToastItem = { ...alert, toastId, createdAt: Date.now() };

      setToasts((prev) => [toast, ...prev].slice(0, 4)); // max 4 toasts

      // Auto-dismiss
      setTimeout(() => dismiss(toastId), TOAST_DURATION);
    },
    [queryClient, dismiss],
  );

  const { connected } = useSignalRAlerts(handleAlert);

  return (
    <>
      {/* Connection indicator — subtle dot in bottom-right */}
      <div className="fixed bottom-4 right-4 z-40 flex items-center gap-1.5 text-xs text-slate-400">
        {connected ? (
          <>
            <span className="h-2 w-2 rounded-full bg-green-500 animate-pulse" />
            <span className="hidden sm:inline">Ao vivo</span>
          </>
        ) : (
          <>
            <WifiOff className="h-3 w-3 text-slate-400" />
            <span className="hidden sm:inline">Offline</span>
          </>
        )}
      </div>

      {/* Toast stack — top-right */}
      <div className="fixed top-20 right-4 z-50 flex flex-col gap-3 w-[340px] max-w-[calc(100vw-2rem)]">
        {toasts.map((toast) => {
          const level = toast.alertLevel.toUpperCase();
          const isCritical = level === 'CRÍTICO' || level === 'CRITICO';

          return (
            <div
              key={toast.toastId}
              className={cn(
                'rounded-xl border shadow-lg p-4 cursor-pointer transition-all animate-in slide-in-from-right-4 fade-in duration-300',
                isCritical
                  ? 'bg-red-50 border-red-300 text-red-900'
                  : 'bg-orange-50 border-orange-300 text-orange-900',
              )}
              onClick={() => {
                router.push(`/votacoes/${toast.sessionId}`);
                dismiss(toast.toastId);
              }}
            >
              <div className="flex items-start gap-3">
                <AlertTriangle
                  className={cn(
                    'h-5 w-5 flex-shrink-0 mt-0.5',
                    isCritical ? 'text-red-500' : 'text-orange-500',
                  )}
                />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span
                      className={cn(
                        'text-xs font-bold uppercase tracking-wide px-1.5 py-0.5 rounded',
                        isCritical ? 'bg-red-200 text-red-800' : 'bg-orange-200 text-orange-800',
                      )}
                    >
                      {toast.alertLevel}
                    </span>
                    <span className="text-xs opacity-70">Score: {toast.score}</span>
                    <span className="text-xs opacity-70">• {toast.chamber}</span>
                  </div>
                  <p className="text-sm font-medium line-clamp-2 leading-snug">
                    {toast.description}
                  </p>
                  {toast.summaryText && (
                    <p className="text-xs mt-1 opacity-70 line-clamp-2">{toast.summaryText}</p>
                  )}
                  <p className="text-xs mt-2 opacity-50">Clique para ver a votação</p>
                </div>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    dismiss(toast.toastId);
                  }}
                  className="p-0.5 rounded hover:bg-black/10 flex-shrink-0"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}

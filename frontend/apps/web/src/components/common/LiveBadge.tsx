'use client';

import { useState, useEffect } from 'react';
import * as signalR from '@microsoft/signalr';
import { cn } from '@/lib/utils';

const HUB_URL =
  (process.env.NEXT_PUBLIC_API_URL ?? 'https://localhost:7001') + '/hubs/plenary';

export function LiveBadge() {
  const [connected, setConnected] = useState<boolean | null>(null);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.None)
      .build();

    conn.onclose(() => setConnected(false));
    conn.onreconnected(() => setConnected(true));

    conn.start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => { conn.stop(); };
  }, []);

  if (connected === null) return null;

  return (
    <span className={cn(
      'inline-flex items-center gap-1.5 text-xs font-medium px-2.5 py-1 rounded-full',
      connected
        ? 'bg-green-100 text-green-700 border border-green-200'
        : 'bg-gray-100 text-gray-500 border border-gray-200',
    )}>
      <span className={cn(
        'h-1.5 w-1.5 rounded-full',
        connected ? 'bg-green-500 animate-pulse' : 'bg-gray-400',
      )} />
      {connected ? 'Ao vivo' : 'Offline'}
    </span>
  );
}

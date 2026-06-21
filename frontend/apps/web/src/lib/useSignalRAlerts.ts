'use client';

import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';

export interface AlertPayload {
  alertId: number;
  sessionId: number;
  externalId: string;
  chamber: string;
  alertLevel: string;
  score: number;
  summaryText: string;
  detectedAt: string;
  description: string;
}

type AlertListener = (alert: AlertPayload) => void;

const HUB_URL =
  (typeof window !== 'undefined' ? '' : '') +
  (process.env.NEXT_PUBLIC_API_URL ?? 'https://localhost:7001') +
  '/hubs/plenary';

export function useSignalRAlerts(onAlert: AlertListener) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [connected, setConnected] = useState(false);
  const onAlertRef = useRef(onAlert);
  onAlertRef.current = onAlert;

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        skipNegotiation: false,
        transport:
          signalR.HttpTransportType.WebSockets |
          signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('ReceiveAlert', (payload: AlertPayload) => {
      onAlertRef.current(payload);
    });

    connection.onclose(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));
    connection.onreconnecting(() => setConnected(false));

    connection
      .start()
      .then(() => {
        setConnected(true);
        console.info('[ChecaAI] Connected to plenary hub');
      })
      .catch((err) => {
        console.warn('[ChecaAI] SignalR connection failed:', err?.message ?? err);
      });

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, []); // only run once

  return { connected };
}

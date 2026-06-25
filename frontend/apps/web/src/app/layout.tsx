import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';
import { Providers } from './providers';
import { Navbar } from '@/components/common/Navbar';
import { AlertsNotifier } from '@/components/common/AlertsNotifier';

const inter = Inter({ subsets: ['latin'], variable: '--font-inter' });

export const metadata: Metadata = {
  title: {
    default: 'ChecaAI — Transparência Política Brasileira',
    template: '%s | ChecaAI',
  },
  description:
    'Plataforma de transparência política: acompanhe votos, despesas, salários e comportamento de todos os parlamentares brasileiros.',
  keywords: ['transparência', 'política', 'parlamentares', 'votações', 'despesas', 'Brasil'],
  openGraph: {
    type: 'website',
    locale: 'pt_BR',
    siteName: 'ChecaAI',
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="pt-BR" className={inter.variable}>
      <body className="min-h-screen flex flex-col">
        <Providers>
          <Navbar />
          <AlertsNotifier />
          <main className="flex-1">{children}</main>
          <footer className="border-t bg-white py-6 mt-12">
            <div className="container mx-auto px-4 text-center text-sm text-gray-500">
              <p>
                ChecaAI — Dados de fontes públicas: Câmara dos Deputados, Senado Federal,
                Portal da Transparência (CGU), TSE.
              </p>
              <p className="mt-1">
                Código aberto • Sem fins lucrativos • Para a cidadania brasileira
              </p>
            </div>
          </footer>
        </Providers>
      </body>
    </html>
  );
}

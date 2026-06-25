import Link from 'next/link';
import { Search, AlertTriangle, BarChart3, Users, FileText, ShieldCheck, Bot } from 'lucide-react';
import { HomeDashboard } from '@/components/home/HomeDashboard';

const features = [
  {
    icon: <Users className="h-6 w-6 text-brand-600" />,
    title: 'Parlamentares',
    description:
      'Perfil completo de senadores, deputados, governadores, prefeitos e vereadores de todo o Brasil.',
    href: '/parlamentares',
  },
  {
    icon: <FileText className="h-6 w-6 text-civic-600" />,
    title: 'Votações',
    description:
      'Acompanhe como cada parlamentar votou em todas as proposições da Câmara e do Senado.',
    href: '/votacoes',
  },
  {
    icon: <BarChart3 className="h-6 w-6 text-brand-600" />,
    title: 'Despesas & Salários',
    description:
      'Cotas parlamentares, salários, auxílios, assessores e gastos de campanha de cada político.',
    href: '/parlamentares',
  },
  {
    icon: <AlertTriangle className="h-6 w-6 text-orange-500" />,
    title: 'Alertas em tempo real',
    description:
      'Detecção automática de votações suspeitas: madrugada, urgência, quórum baixo.',
    href: '/alertas',
  },
  {
    icon: <ShieldCheck className="h-6 w-6 text-civic-600" />,
    title: 'Transparência total',
    description:
      'Dados do Portal da Transparência CGU, TSE, Câmara e Senado — atualizados continuamente.',
    href: '/parlamentares',
  },
  {
    icon: <Bot className="h-6 w-6 text-brand-600" />,
    title: 'IA para explicar',
    description:
      'Claude AI analisa votações e comportamento parlamentar em linguagem simples para qualquer cidadão.',
    href: '/busca',
  },
];

export default function HomePage() {
  return (
    <>
      {/* Hero */}
      <section className="bg-gradient-to-br from-brand-700 via-brand-600 to-civic-700 text-white py-20 px-4">
        <div className="container mx-auto max-w-4xl text-center">
          <div className="inline-flex items-center gap-2 bg-white/10 text-brand-100 text-sm px-4 py-1.5 rounded-full mb-6 border border-white/20">
            <span className="h-2 w-2 rounded-full bg-green-400 animate-pulse" />
            Monitorando Câmara e Senado em tempo real
          </div>
          <h1 className="text-4xl md:text-6xl font-extrabold mb-6 leading-tight">
            Transparência política
            <br />
            <span className="text-brand-200">ao alcance de todos</span>
          </h1>
          <p className="text-lg md:text-xl text-brand-100 mb-10 max-w-2xl mx-auto">
            Acompanhe votações, despesas, salários e o comportamento de qualquer
            parlamentar brasileiro — tudo em um só lugar, com IA para explicar.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link
              href="/parlamentares"
              className="bg-white text-brand-700 font-semibold px-8 py-3 rounded-lg hover:bg-brand-50 transition-colors shadow-lg"
            >
              Ver parlamentares
            </Link>
            <Link
              href="/busca"
              className="flex items-center justify-center gap-2 bg-white/10 border-2 border-white/30 text-white font-semibold px-8 py-3 rounded-lg hover:bg-white/20 transition-colors"
            >
              <Bot className="h-4 w-4" />
              Perguntar à IA
            </Link>
          </div>
        </div>
      </section>

      {/* Live dashboard — stats, alerts, sessions */}
      <HomeDashboard />

      {/* Features */}
      <section className="py-16 px-4 bg-gray-50 border-t">
        <div className="container mx-auto max-w-5xl">
          <h2 className="text-3xl font-bold text-center mb-3 text-gray-900">
            O que você pode fazer no Checa Aí
          </h2>
          <p className="text-center text-gray-500 mb-10 text-sm">
            Dados de fontes oficiais, atualizados automaticamente, prontos para o cidadão.
          </p>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-5">
            {features.map((f) => (
              <Link
                key={f.title}
                href={f.href}
                className="group bg-white rounded-xl p-6 border hover:border-brand-300 hover:shadow-md transition-all"
              >
                <div className="mb-3">{f.icon}</div>
                <h3 className="font-semibold text-gray-900 mb-2 group-hover:text-brand-700">
                  {f.title}
                </h3>
                <p className="text-sm text-gray-600 leading-relaxed">{f.description}</p>
              </Link>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="bg-gradient-to-r from-civic-600 to-brand-700 py-16 px-4">
        <div className="container mx-auto max-w-2xl text-center text-white">
          <h2 className="text-2xl font-bold mb-4">
            Pesquise qualquer parlamentar
          </h2>
          <p className="text-civic-100 mb-8">
            Busque por nome, partido, estado ou cargo. Veja votos, despesas e receba uma
            análise com IA em segundos.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link
              href="/parlamentares"
              className="bg-white text-brand-700 font-semibold px-10 py-3 rounded-lg hover:bg-gray-100 transition-colors"
            >
              Começar a pesquisar →
            </Link>
            <Link
              href="/alertas"
              className="border-2 border-white text-white font-semibold px-10 py-3 rounded-lg hover:bg-white/10 transition-colors"
            >
              Ver alertas
            </Link>
          </div>
        </div>
      </section>
    </>
  );
}

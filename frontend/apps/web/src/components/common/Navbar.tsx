'use client';

import Link from 'next/link';
import Image from 'next/image';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import { useState } from 'react';
import { Menu, X, Sparkles } from 'lucide-react';

const navLinks = [
  { href: '/parlamentares',  label: 'Parlamentares' },
  { href: '/votacoes',       label: 'Votações' },
  { href: '/proposicoes',    label: 'Proposições' },
  { href: '/comparar',       label: 'Comparar' },
  { href: '/partidos',       label: 'Partidos' },
  { href: '/alertas',        label: 'Ao Vivo' },
];

export function Navbar() {
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <header className="sticky top-0 z-50 border-b bg-white/95 backdrop-blur-sm shadow-sm">
      <div className="container mx-auto px-4">
        <div className="flex h-20 items-center justify-between">
          {/* Logo */}
          <Link href="/" className="flex items-center">
            <Image src="/logo.png" alt="ChecaAI" width={280} height={140} className="h-16 w-auto mix-blend-multiply" priority />
          </Link>

          {/* Desktop nav */}
          <nav className="hidden md:flex items-center gap-1">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className={cn(
                  'px-3 py-2 rounded-md text-sm font-medium transition-colors',
                  pathname.startsWith(link.href)
                    ? 'bg-primary-50 text-primary-700'
                    : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
                )}
              >
                {link.label}
              </Link>
            ))}
          </nav>

          {/* AI Search + mobile toggle */}
          <div className="flex items-center gap-2">
            <Link
              href="/busca"
              className={cn(
                'hidden sm:flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors',
                pathname.startsWith('/busca')
                  ? 'bg-primary-100 text-primary-700'
                  : 'bg-primary-50 text-primary-700 border border-primary-200 hover:bg-primary-100',
              )}
            >
              <Sparkles className="h-4 w-4" />
              Busca IA
            </Link>
            <button
              className="md:hidden p-2 rounded-md text-slate-600 hover:bg-slate-100"
              onClick={() => setMobileOpen(!mobileOpen)}
              aria-label="Menu"
            >
              {mobileOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile menu */}
      {mobileOpen && (
        <div className="md:hidden border-t bg-white px-4 py-3 space-y-1">
          {navLinks.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              onClick={() => setMobileOpen(false)}
              className={cn(
                'block px-3 py-2 rounded-md text-sm font-medium',
                pathname.startsWith(link.href)
                  ? 'bg-primary-50 text-primary-700'
                  : 'text-slate-600 hover:bg-slate-100',
              )}
            >
              {link.label}
            </Link>
          ))}
          <Link
            href="/busca"
            onClick={() => setMobileOpen(false)}
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-md text-sm font-medium',
              pathname.startsWith('/busca')
                ? 'bg-primary-100 text-primary-700'
                : 'text-primary-700 hover:bg-primary-50',
            )}
          >
            <Sparkles className="h-4 w-4" />
            Busca IA
          </Link>
        </div>
      )}
    </header>
  );
}

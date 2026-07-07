import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** Format a currency value in BRL */
export function formatBRL(value: number): string {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
    minimumFractionDigits: 2,
  }).format(value);
}

/** Format a date string (ISO) to pt-BR format */
export function formatDate(dateStr: string | null | undefined): string {
  if (!dateStr) return '—';
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });
  } catch {
    return dateStr;
  }
}

/** Format a month/year pair */
export function formatMonthYear(month: number, year: number): string {
  const months = [
    'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun',
    'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez',
  ];
  return `${months[month - 1]} ${year}`;
}

/** Presence rate as a badge color */
export function presenceBadgeColor(rate: number): string {
  if (rate >= 80) return 'bg-green-100 text-green-800';
  if (rate >= 60) return 'bg-yellow-100 text-yellow-800';
  return 'bg-red-100 text-red-800';
}

/** Alert level as a Tailwind color variant */
export function alertLevelColor(level: string): string {
  switch (level) {
    case 'Crítico': return 'text-red-600 bg-red-50 border-red-200';
    case 'Atenção':  return 'text-orange-600 bg-orange-50 border-orange-200';
    default:         return 'text-green-600 bg-green-50 border-green-200';
  }
}

/** Session result → Portuguese label */
export function resultLabel(result: string): string {
  const r = result.toLowerCase();
  if (r.includes('approved') || r.includes('aprovad')) return 'Aprovada';
  if (r.includes('rejected') || r.includes('rejeitad')) return 'Rejeitada';
  if (r.includes('prejudicad')) return 'Prejudicada';
  if (r.includes('withdrawn') || r.includes('retirad')) return 'Retirada';
  if (r.includes('archived') || r.includes('arquivad')) return 'Arquivada';
  return result;
}

/** Whether a result string means the proposal was approved */
export function isApproved(result: string): boolean {
  const r = result.toLowerCase();
  return r.includes('approved') || r.includes('aprovad');
}

/** Session result → color class */
export function resultColor(result: string): string {
  const r = result.toLowerCase();
  if (r.includes('approved') || r.includes('aprovad')) return 'bg-green-100 text-green-800';
  if (r.includes('rejected') || r.includes('rejeitad')) return 'bg-red-100 text-red-800';
  if (r.includes('prejudicad') || r.includes('withdrawn') || r.includes('retirad') || r.includes('arquivad')) return 'bg-slate-100 text-slate-700';
  return 'bg-blue-100 text-blue-800';
}

/** Vote value → background color class */
export function voteColor(value: string): string {
  switch (value) {
    case 'Yes':        return 'bg-green-100 text-green-800';
    case 'No':         return 'bg-red-100 text-red-800';
    case 'Abstention': return 'bg-orange-100 text-orange-800';
    default:           return 'bg-slate-100 text-slate-700';
  }
}

/** Vote value → Portuguese label */
export function voteLabel(value: string): string {
  const map: Record<string, string> = {
    Yes: 'Sim', No: 'Não', Abstention: 'Abstenção', Absent: 'Ausente',
  };
  return map[value] ?? value;
}

/** Political position → Portuguese label */
export function positionLabel(pos: string): string {
  const map: Record<string, string> = {
    'Federal Deputy':  'Deputado Federal',
    'Senator':         'Senador',
    'Governor':        'Governador',
    'Mayor':           'Prefeito',
    'State Deputy':    'Deputado Estadual',
    'City Councilor':  'Vereador',
    'President':       'Presidente',
  };
  return map[pos] ?? pos;
}

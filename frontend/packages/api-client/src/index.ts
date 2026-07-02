import type {
  PagedResponse,
  PoliticianListItem,
  PoliticianDetail,
  Expense,
  Salary,
  RecentVote,
  Committee,
  CommitteeMembership,
  Proposal,
  ProposalDetail,
  VotingSession,
  Party,
  Alert,
  AllowanceSummary,
  CabinetStaff,
  ElectionResult,
  CampaignExpense,
  AttendanceRecord,
  PoliticianFilters,
  SessionFilters,
  ProposalFilters,
  ChatMessage,
} from '@checaai/types';

// ── Base config ───────────────────────────────────────────────────────────────

const DEFAULT_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? 'https://localhost:7001';

async function apiFetch<T>(
  path: string,
  options?: RequestInit,
  baseUrl = DEFAULT_BASE_URL,
): Promise<T> {
  const res = await fetch(`${baseUrl}${path}`, {
    headers: { 'Content-Type': 'application/json', ...options?.headers },
    ...options,
  });

  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`API ${res.status}: ${text || res.statusText}`);
  }

  return res.json() as Promise<T>;
}

function buildQuery(params: Record<string, unknown>): string {
  const q = new URLSearchParams();
  for (const [key, val] of Object.entries(params)) {
    if (val !== undefined && val !== null && val !== '') {
      q.set(key, String(val));
    }
  }
  const str = q.toString();
  return str ? `?${str}` : '';
}

// ── Politicians ───────────────────────────────────────────────────────────────

export const politiciansApi = {
  list: (filters: PoliticianFilters = {}) =>
    apiFetch<PagedResponse<PoliticianListItem>>(
      `/api/politicians${buildQuery(filters as Record<string, unknown>)}`,
    ),

  get: (id: number) =>
    apiFetch<PoliticianDetail>(`/api/politicians/${id}`),

  votes: (id: number, page = 1, pageSize = 20) =>
    apiFetch<PagedResponse<RecentVote>>(
      `/api/politicians/${id}/votes${buildQuery({ page, pageSize })}`,
    ),

  expenses: (id: number, year?: number, category?: string, page = 1, pageSize = 20) =>
    apiFetch<PagedResponse<Expense>>(
      `/api/politicians/${id}/expenses${buildQuery({ year, category, page, pageSize })}`,
    ),

  salaries: (id: number) =>
    apiFetch<Salary[]>(`/api/politicians/${id}/salaries`),
};

// ── Proposals ─────────────────────────────────────────────────────────────────

export const proposalsApi = {
  list: (filters: ProposalFilters = {}) =>
    apiFetch<PagedResponse<Proposal>>(
      `/api/proposals${buildQuery(filters as Record<string, unknown>)}`,
    ),

  get: (id: number) =>
    apiFetch<ProposalDetail>(`/api/proposals/${id}`),
};

// ── Voting Sessions ───────────────────────────────────────────────────────────

export const sessionsApi = {
  list: (filters: SessionFilters = {}) =>
    apiFetch<PagedResponse<VotingSession>>(
      `/api/sessions${buildQuery(filters as Record<string, unknown>)}`,
    ),

  get: (id: number) =>
    apiFetch<VotingSession & { votes: unknown[]; proposal: unknown }>(`/api/sessions/${id}`),
};

// ── Committees ────────────────────────────────────────────────────────────────

export const committeesApi = {
  list: (chamber?: string, type?: string, active?: boolean, page = 1, pageSize = 20) =>
    apiFetch<PagedResponse<Committee>>(
      `/api/committees${buildQuery({ chamber, type, active, page, pageSize })}`,
    ),

  get: (id: number) =>
    apiFetch<Committee & { members: unknown[] }>(`/api/committees/${id}`),

  byPolitician: (politicianId: number) =>
    apiFetch<CommitteeMembership[]>(`/api/committees/politicians/${politicianId}`),
};

// ── Parties ───────────────────────────────────────────────────────────────────

export const partiesApi = {
  list: (active?: boolean) =>
    apiFetch<Party[]>(`/api/parties${buildQuery({ active })}`),

  get: (acronym: string) =>
    apiFetch<Party>(`/api/parties/${acronym}`),

  members: (acronym: string, position?: string, page = 1, pageSize = 20) =>
    apiFetch<PagedResponse<PoliticianListItem>>(
      `/api/parties/${acronym}/members${buildQuery({ position, page, pageSize })}`,
    ),
};

// ── Alerts ────────────────────────────────────────────────────────────────────

export const alertsApi = {
  list: (limit = 20, chamber?: string) =>
    apiFetch<Alert[]>(`/api/alerts${buildQuery({ limit, chamber })}`),

  get: (id: number) =>
    apiFetch<Alert>(`/api/alerts/${id}`),
};

// ── Transparency ──────────────────────────────────────────────────────────────

export const transparencyApi = {
  salary: (id: number, year?: number, month?: number) =>
    apiFetch<Salary[]>(
      `/api/transparency/politicians/${id}/salary${buildQuery({ year, month })}`,
    ),

  allowances: (id: number, year?: number, page = 1, pageSize = 12) =>
    apiFetch<PagedResponse<AllowanceSummary>>(
      `/api/transparency/politicians/${id}/allowances${buildQuery({ year, page, pageSize })}`,
    ),

  cabinetStaff: (id: number, year?: number, month?: number, page = 1, pageSize = 20) =>
    apiFetch<PagedResponse<CabinetStaff>>(
      `/api/transparency/politicians/${id}/cabinet-staff${buildQuery({ year, month, page, pageSize })}`,
    ),

  campaignExpenses: (id: number, year?: number) =>
    apiFetch<CampaignExpense[]>(
      `/api/transparency/politicians/${id}/campaign-expenses${buildQuery({ year })}`,
    ),

  assets: (id: number, year?: number) =>
    apiFetch<unknown[]>(
      `/api/transparency/politicians/${id}/assets${buildQuery({ year })}`,
    ),

  electionResults: (id: number) =>
    apiFetch<ElectionResult[]>(`/api/transparency/politicians/${id}/election-results`),

  attendance: (id: number, year?: number, page = 1, pageSize = 20) =>
    apiFetch<PagedResponse<AttendanceRecord>>(
      `/api/transparency/politicians/${id}/attendance${buildQuery({ year, page, pageSize })}`,
    ),
};

// ── AI ────────────────────────────────────────────────────────────────────────

export const aiApi = {
  /** Returns a streaming response — caller should read as text/event-stream */
  streamUrl: (endpoint: string) => `${DEFAULT_BASE_URL}${endpoint}`,

  politicianAnalysis: (id: number) =>
    `${DEFAULT_BASE_URL}/api/ai/politician/${id}/analysis`,

  proposalSummary: (id: number) =>
    `${DEFAULT_BASE_URL}/api/ai/proposal/${id}/summary`,

  sessionExplanation: (id: number) =>
    `${DEFAULT_BASE_URL}/api/ai/session/${id}/explain`,

  compare: (id1: number, id2: number) =>
    `${DEFAULT_BASE_URL}/api/ai/compare?id1=${id1}&id2=${id2}`,

  chat: async (body: ChatMessage) =>
    fetch(`${DEFAULT_BASE_URL}/api/ai/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
};

export type { PagedResponse } from '@checaai/types';

// ── Generic ────────────────────────────────────────────────────────────────────

export interface PagedResponse<T> {
  data: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPrevPage: boolean;
}

// ── Politician ─────────────────────────────────────────────────────────────────

export interface PoliticianListItem {
  id: number;
  fullName: string;
  politicalPosition: string;
  party: string | null;
  state: string | null;
  city: string | null;
  photoUrl: string | null;
  isActive: boolean;
}

export interface PoliticianDetail extends PoliticianListItem {
  email: string | null;
  website: string | null;
  externalId: string | null;
  voteStats: VoteStats | null;
  recentVotes: RecentVote[];
  expenseSummary: ExpenseSummary | null;
  committees: CommitteeMembership[];
  latestSalary: Salary | null;
}

export interface VoteStats {
  total: number;
  yes: number;
  no: number;
  abstention: number;
  absent: number;
  presenceRate: number;
}

export interface RecentVote {
  sessionId: number;
  voteValue: 'Yes' | 'No' | 'Abstention' | 'Absent';
  votingDate: string;
  description: string;
  result: string;
  chamber: string;
}

export interface ExpenseSummary {
  year: number;
  total: number;
  count: number;
  byCategory: ExpenseCategory[];
}

export interface ExpenseCategory {
  category: string;
  total: number;
  count: number;
}

export interface Expense {
  id: number;
  description: string;
  category: string;
  amount: number;
  provider: string | null;
  documentNumber: string | null;
  expenseDate: string;
  month: string | null;
  year: number;
}

export interface Salary {
  id: number;
  grossSalary: number;
  netSalary: number;
  allowances: number;
  month: number;
  year: number;
  source: string | null;
}

// ── Committee ─────────────────────────────────────────────────────────────────

export interface Committee {
  id: number;
  name: string;
  acronym: string | null;
  committeeType: string;
  chamber: string;
  isActive: boolean;
  memberCount: number;
}

export interface CommitteeMembership {
  committeeId: number;
  committeeName: string;
  acronym: string | null;
  committeeType: string;
  chamber: string;
  role: string;
}

// ── Proposal ──────────────────────────────────────────────────────────────────

export interface Proposal {
  id: number;
  externalId: string;
  title: string;
  summary: string | null;
  type: string;
  number: string | null;
  year: number;
  chamber: string;
  author: string | null;
  status: string;
  proposalDate: string | null;
  votingSessionCount: number;
}

export interface ProposalDetail extends Proposal {
  votingSessions: VotingSession[];
}

// ── VotingSession ─────────────────────────────────────────────────────────────

export interface VotingSession {
  id: number;
  externalId: string;
  description: string;
  votingDate: string;
  sessionType: string | null;
  totalVotes: number;
  votesYes: number;
  votesNo: number;
  votesAbstention: number;
  votesAbsent: number;
  result: string;
  chamber: string;
  proposalId: number | null;
  proposalTitle: string | null;
  proposalType: string | null;
  hasAlert: boolean;
}

// ── Party ─────────────────────────────────────────────────────────────────────

export interface Party {
  id: number;
  acronym: string;
  fullName: string;
  number: number | null;
  president: string | null;
  isActive: boolean;
  memberCount: number;
}

// ── Alert ─────────────────────────────────────────────────────────────────────

export interface Alert {
  id: number;
  votingSessionId: number;
  externalId: string;
  chamber: string;
  description: string;
  votingDate: string;
  alertLevel: 'Normal' | 'Atenção' | 'Crítico';
  score: number;
  scoreBreakdown: string | null;
  summaryText: string | null;
  detectedAt: string;
  signalRSent: boolean;
  pushSent: boolean;
}

// ── Transparency ──────────────────────────────────────────────────────────────

export interface AllowanceItem {
  id: number;
  allowanceType: string;
  amount: number;
  month: number;
  year: number;
  description: string | null;
  source: string | null;
}

export interface AllowanceSummary {
  year: number;
  month: number;
  total: number;
  items: AllowanceItem[];
}

export interface CabinetStaff {
  id: number;
  fullName: string;
  role: string | null;
  grossSalary: number;
  netSalary: number;
  month: number | null;
  year: number | null;
  startDate: string | null;
  endDate: string | null;
}

export interface ElectionResult {
  id: number;
  electionYear: number;
  position: string;
  state: string | null;
  city: string | null;
  votesReceived: number;
  totalVotes: number;
  voteShare: number;
  isElected: boolean;
  externalId: string | null;
}

export interface CampaignExpense {
  id: number;
  electionYear: number;
  category: string;
  description: string | null;
  amount: number;
  supplier: string | null;
  supplierCnpjCpf: string | null;
  externalId: string | null;
}

export interface AttendanceRecord {
  id: number;
  sessionDate: string;
  isPresent: boolean;
  absenceReason: string | null;
  absenceJustification: string | null;
  chamber: string;
}

// ── AI ────────────────────────────────────────────────────────────────────────

export interface ChatMessage {
  message: string;
  politicianId?: number;
  proposalId?: number;
}

// ── Filter params ─────────────────────────────────────────────────────────────

export interface PoliticianFilters {
  q?: string;
  position?: string;
  state?: string;
  party?: string;
  page?: number;
  pageSize?: number;
}

export interface SessionFilters {
  chamber?: string;
  from?: string;
  to?: string;
  result?: string;
  hasAlert?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ProposalFilters {
  q?: string;
  type?: string;
  year?: number;
  chamber?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

// ── Constants ─────────────────────────────────────────────────────────────────

export const POLITICAL_POSITIONS = [
  'Federal Deputy',
  'Senator',
  'Governor',
  'Mayor',
  'State Deputy',
  'City Councilor',
  'President',
] as const;

export type PoliticalPosition = typeof POLITICAL_POSITIONS[number];

export const POSITION_LABELS: Record<string, string> = {
  'Federal Deputy': 'Deputado Federal',
  'Senator': 'Senador',
  'Governor': 'Governador',
  'Mayor': 'Prefeito',
  'State Deputy': 'Deputado Estadual',
  'City Councilor': 'Vereador',
  'President': 'Presidente',
};

export const VOTE_LABELS: Record<string, string> = {
  'Yes': 'Sim',
  'No': 'Não',
  'Abstention': 'Abstenção',
  'Absent': 'Ausente',
};

export const CHAMBER_LABELS: Record<string, string> = {
  'Câmara': 'Câmara dos Deputados',
  'Senado': 'Senado Federal',
};

export const BR_STATES = [
  'AC', 'AL', 'AM', 'AP', 'BA', 'CE', 'DF', 'ES', 'GO',
  'MA', 'MG', 'MS', 'MT', 'PA', 'PB', 'PE', 'PI', 'PR',
  'RJ', 'RN', 'RO', 'RR', 'RS', 'SC', 'SE', 'SP', 'TO',
] as const;

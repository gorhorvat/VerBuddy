// Typed client for the Backend API. GDPR note: student-facing types carry only
// DisplayName/XP; the *Admin types (with real names) come from Teacher-only endpoints.

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5247'

// ─── Types (camelCase mirrors of the backend DTOs) ─────────────────────────

export type AssignmentType = 'SingleChoice' | 'MultipleChoice' | 'FillInTheBlanks' | 'Matching'
export type AssignmentState = 'Draft' | 'Active' | 'Closed'
export type AttemptStatus = 'InProgress' | 'Completed' | 'PendingReview' | 'Invalidated'

export interface AuthResponse {
  token: string
  expiresAtUtc: string
  displayName: string
  totalXp: number
  roles: string[]
  mustChangePassword: boolean
}

export interface MeResponse {
  displayName: string
  totalXp: number
  /** Computed server-side from totalXp; the client mirrors the math in lib/levels.ts. */
  level: number
  roles: string[]
  mustChangePassword: boolean
}

export type RewardApplicationStatus = 'Pending' | 'Approved' | 'Denied'

export interface Reward {
  id: number
  title: string
  description: string | null
  requiredLevel: number
}

export interface StudentReward extends Reward {
  unlocked: boolean
  myApplicationStatus: RewardApplicationStatus | null
}

export interface RewardApplication {
  id: number
  rewardId: number
  rewardTitle: string
  requiredLevel: number
  studentDisplayName: string
  status: RewardApplicationStatus
  createdAtUtc: string
  decidedAtUtc: string | null
}

export interface Category {
  id: number
  name: string
  assignmentCount: number
  studentCount: number
}

export interface StudentAssignmentSummary {
  id: number
  title: string
  description: string | null
  assignmentType: AssignmentType
  state: AssignmentState
  timeLimitSeconds: number | null
  xpReward: number
  questionCount: number
  categoryId: number | null
  categoryName: string | null
  myStatus: 'NotStarted' | AttemptStatus
  myScore: number | null
  myMaxScore: number | null
  myEarnedXp: number | null
}

export interface StudentQuestion {
  id: number
  order: number
  prompt: string
  points: number
  jsonContent: string
}

export interface StartAttempt {
  attemptId: number
  startedAtUtc: string
  timeLimitSeconds: number | null
  deadlineUtc: string | null
  questions: StudentQuestion[]
}

export interface AttemptResult {
  attemptId: number
  status: AttemptStatus
  score: number
  maxScore: number
  earnedXp: number
  submittedAtUtc: string | null
  teacherFeedback: string | null
}

export interface LeaderboardEntry {
  rank: number
  displayName: string
  totalXp: number
}

export interface LeaderboardCategory {
  id: number
  name: string
  entries: LeaderboardEntry[]
}

export interface Leaderboards {
  categories: LeaderboardCategory[]
  globalEntries: LeaderboardEntry[]
}

export interface AnswerBreakdown {
  questionId: number
  order: number
  prompt: string
  points: number
  /** Full question content INCLUDING the answer key. */
  contentJson: string
  /** The student's raw answer object, null when unanswered. */
  answer: unknown
  autoPoints: number
  finalPoints: number
  isOverridden: boolean
}

export interface AttemptAnswers {
  attemptId: number
  studentDisplayName: string
  studentFirstName: string | null
  studentLastName: string | null
  status: AttemptStatus
  score: number
  maxScore: number
  earnedXp: number
  submittedAtUtc: string | null
  answers: AnswerBreakdown[]
}

export interface AssignmentAnswers {
  assignmentId: number
  title: string
  assignmentType: AssignmentType
  xpReward: number
  attempts: AttemptAnswers[]
}

export interface MyAnswers {
  assignmentId: number
  title: string
  assignmentType: AssignmentType
  result: AttemptResult
  answers: AnswerBreakdown[]
}

export interface AssignmentSummary {
  id: number
  title: string
  description: string | null
  assignmentType: AssignmentType
  state: AssignmentState
  timeLimitSeconds: number | null
  xpReward: number
  requireFeedback: boolean
  categoryId: number | null
  categoryName: string | null
  createdAt: string
  questionCount: number
  attemptCount: number
  attemptDisplayNames: string[]
}

export interface QuestionAdmin {
  id: number
  order: number
  prompt: string
  points: number
  jsonContent: string
}

export interface AssignmentDetail extends Omit<AssignmentSummary, 'questionCount' | 'attemptDisplayNames'> {
  questions: QuestionAdmin[]
}

export interface AttemptAdmin {
  id: number
  assignmentId: number
  assignmentTitle: string
  assignmentType: AssignmentType
  studentDisplayName: string
  studentFirstName: string | null
  studentLastName: string | null
  status: AttemptStatus
  score: number
  maxScore: number
  earnedXp: number
  startedAtUtc: string
  submittedAtUtc: string | null
  answersJson: string | null
  teacherFeedback: string | null
}

export interface StudentAdmin {
  id: string
  username: string
  firstName: string | null
  lastName: string | null
  email: string | null
  displayName: string
  totalXp: number
  categories: { id: number; name: string }[]
  isActive: boolean
  activatedAt: string | null
  mustChangePassword: boolean
}

export interface AdminAccount {
  id: string
  username: string
  firstName: string | null
  lastName: string | null
  email: string | null
  displayName: string
  isActive: boolean
  activatedAt: string | null
  mustChangePassword: boolean
}

export interface ImportStudentRow {
  username: string
  firstName: string | null
  lastName: string | null
  email: string | null
  displayName: string | null
}

export interface ImportStudentsResult {
  created: StudentAdmin[]
  errors: string[]
}

export interface BulkActivateResult {
  activated: number
  errors: string[]
}

// ─── Client ────────────────────────────────────────────────────────────────

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message)
  }
}

let authToken: string | null = null
export function setAuthToken(token: string | null) {
  authToken = token
}

export async function api<T>(
  path: string,
  options: { method?: string; body?: unknown } = {},
): Promise<T> {
  const headers: Record<string, string> = {}
  if (authToken) headers.Authorization = `Bearer ${authToken}`
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  const res = await fetch(`${API_BASE}${path}`, {
    method: options.method ?? 'GET',
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  })

  if (!res.ok) {
    let message = res.statusText
    try {
      const data = await res.json()
      message = data.message ?? (data.errors ? Object.values(data.errors).flat().join(' ') : message)
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError(res.status, message)
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

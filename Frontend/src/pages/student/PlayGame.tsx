import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type AttemptResult, type GameType, type StartAttempt } from '../../api'
import { Button, Card, ErrorText, Spinner } from '../../components/ui'
import {
  FillInTheBlanksInput,
  MultipleChoiceInput,
  SingleChoiceInput,
  WordMatchingInput,
} from '../../components/GameInputs'

const inputByType: Record<GameType, typeof SingleChoiceInput> = {
  SingleChoice: SingleChoiceInput,
  MultipleChoice: MultipleChoiceInput,
  FillInTheBlanks: FillInTheBlanksInput,
  WordMatching: WordMatchingInput,
}

/** The game type travels via the dashboard link state-free: we re-read it from the list. */
function useGameType(id: string): GameType | null {
  const [type, setType] = useState<GameType | null>(null)
  useEffect(() => {
    api<{ id: number; gameType: GameType }[]>('/api/student/games').then((games) => {
      const game = games.find((g) => g.id === Number(id))
      if (game) setType(game.gameType)
    })
  }, [id])
  return type
}

function CountdownBar({ deadline, total, onExpire }: { deadline: string; total: number; onExpire: () => void }) {
  const [secondsLeft, setSecondsLeft] = useState(() =>
    Math.max(0, (new Date(deadline).getTime() - Date.now()) / 1000),
  )
  const expiredRef = useRef(false)

  useEffect(() => {
    const timer = setInterval(() => {
      const left = Math.max(0, (new Date(deadline).getTime() - Date.now()) / 1000)
      setSecondsLeft(left)
      if (left <= 0 && !expiredRef.current) {
        expiredRef.current = true
        clearInterval(timer)
        onExpire()
      }
    }, 250)
    return () => clearInterval(timer)
  }, [deadline, onExpire])

  const pct = Math.max(0, Math.min(100, (secondsLeft / total) * 100))
  const urgent = secondsLeft <= 30
  const m = Math.floor(secondsLeft / 60)
  const s = Math.floor(secondsLeft % 60)

  return (
    <div className="sticky top-14 z-10 rounded-xl border border-white/10 bg-[#0d0d0d]/90 p-3 backdrop-blur">
      <div className="mb-1 flex justify-between text-xs font-semibold">
        <span className={urgent ? 'text-rose-600' : 'text-slate-500'}>Time remaining</span>
        <span className={`tabular-nums ${urgent ? 'animate-pulse text-rose-600' : 'text-slate-700'}`}>
          {m}:{s.toString().padStart(2, '0')}
        </span>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-slate-200">
        <div
          className={`h-full rounded-full transition-[width] duration-300 ${urgent ? 'bg-rose-500' : 'bg-indigo-500'}`}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  )
}

export default function PlayGame() {
  const { id } = useParams() as { id: string }
  const gameType = useGameType(id)
  const [start, setStart] = useState<StartAttempt | null>(null)
  const [answers, setAnswers] = useState<Record<number, unknown>>({})
  const [result, setResult] = useState<AttemptResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const submittedRef = useRef(false)
  const startRequestedForRef = useRef<string | null>(null)

  useEffect(() => {
    // POST /start is a mutation, so it must not re-fire when StrictMode
    // re-runs the effect in dev; the ref survives the remount cycle.
    if (startRequestedForRef.current === id) return
    startRequestedForRef.current = id
    api<StartAttempt>(`/api/student/games/${id}/start`, { method: 'POST' })
      .then(setStart)
      .catch((e) => setError(e.message))
  }, [id])

  const submit = useCallback(async () => {
    if (submittedRef.current) return
    submittedRef.current = true
    setSubmitting(true)
    try {
      const payload = {
        answers: Object.entries(answers).map(([questionId, answer]) => ({
          questionId: Number(questionId),
          answer,
        })),
      }
      setResult(await api<AttemptResult>(`/api/student/games/${id}/submit`, { method: 'POST', body: payload }))
    } catch (e) {
      submittedRef.current = false
      setError(e instanceof Error ? e.message : 'Submission failed.')
    } finally {
      setSubmitting(false)
    }
  }, [answers, id])

  if (error && !result) {
    return (
      <div className="space-y-3">
        <ErrorText message={error} />
        <Link to="/games" className="text-sm font-semibold text-indigo-600">← Back to games</Link>
      </div>
    )
  }

  // ── Result screen ────────────────────────────────────────────────────────
  if (result) {
    const perfect = result.score === result.maxScore
    return (
      <Card className="space-y-4 text-center">
        <p className="text-5xl">
          {result.status === 'Invalidated' ? '⏰' : perfect ? '🎉' : result.status === 'PendingReview' ? '🕵️' : '👍'}
        </p>
        {result.status === 'Invalidated' ? (
          <>
            <h1 className="text-xl font-bold text-rose-700">Time's up!</h1>
            <p className="text-sm text-slate-500">
              Your answers arrived after the time limit, so this attempt doesn't count.
            </p>
          </>
        ) : (
          <>
            <h1 className="text-xl font-bold">
              {result.score} / {result.maxScore}
            </h1>
            <p className="inline-block rounded-full bg-indigo-50 px-4 py-1.5 font-bold text-indigo-700">
              +{result.earnedXp} XP
            </p>
            {result.status === 'PendingReview' && (
              <p className="rounded-xl bg-amber-50 px-4 py-3 text-sm text-amber-800">
                Your answers will be reviewed by your teacher — your score and XP may still change!
              </p>
            )}
          </>
        )}
        <div className="flex flex-col gap-2 sm:flex-row sm:justify-center">
          <Link to="/games"><Button variant="secondary" className="w-full sm:w-auto">Back to games</Button></Link>
          <Link to="/leaderboard"><Button className="w-full sm:w-auto">View leaderboard</Button></Link>
        </div>
      </Card>
    )
  }

  if (!start || !gameType) return <Spinner />

  const AnswerInput = inputByType[gameType]
  const answeredCount = Object.keys(answers).length

  return (
    <div className="space-y-4">
      {start.deadlineUtc && start.timeLimitSeconds && (
        <CountdownBar deadline={start.deadlineUtc} total={start.timeLimitSeconds} onExpire={submit} />
      )}

      {start.questions.map((q, index) => (
        <Card key={q.id} className="space-y-3">
          <div className="flex items-baseline justify-between gap-2">
            <h2 className="font-bold">
              <span className="mr-2 text-slate-400">{index + 1}.</span>
              {q.prompt}
            </h2>
            <span className="shrink-0 text-xs font-semibold text-slate-400">
              {q.points} pt{q.points === 1 ? '' : 's'}
            </span>
          </div>
          <AnswerInput
            jsonContent={q.jsonContent}
            value={answers[q.id]}
            onChange={(answer) => setAnswers((prev) => ({ ...prev, [q.id]: answer }))}
          />
        </Card>
      ))}

      <Button
        onClick={submit}
        disabled={submitting}
        variant="success"
        className="w-full py-3.5 text-base"
      >
        {submitting
          ? 'Submitting…'
          : `Submit answers (${answeredCount}/${start.questions.length} answered)`}
      </Button>
    </div>
  )
}

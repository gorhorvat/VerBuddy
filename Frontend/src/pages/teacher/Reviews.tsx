import { useEffect, useState } from 'react'
import { api, type AnswerBreakdown, type AttemptAdmin, type GameAnswers } from '../../api'
import { Badge, Button, Card, ErrorText, Spinner, inputClass } from '../../components/ui'
import AnswerView from '../../components/AnswerView'

// Pending attempts only carry the raw answersJson blob, but the admin
// "answers" endpoint (already used by GameAnswers.tsx) returns every
// attempt's answers resolved against the full question content — including
// the answer key — so we can reuse AnswerView instead of duplicating its
// choice/blank/pair rendering here. Cache per game so a review list with
// several pending attempts on the same game only fetches it once.
const gameAnswersCache = new Map<number, Promise<GameAnswers>>()

function fetchGameAnswers(gameInstanceId: number): Promise<GameAnswers> {
  let cached = gameAnswersCache.get(gameInstanceId)
  if (!cached) {
    cached = api<GameAnswers>(`/api/admin/games/${gameInstanceId}/answers`)
    gameAnswersCache.set(gameInstanceId, cached)
  }
  return cached
}

function AttemptAnswersView({ attempt }: { attempt: AttemptAdmin }) {
  const [answers, setAnswers] = useState<AnswerBreakdown[] | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)

  useEffect(() => {
    let cancelled = false
    fetchGameAnswers(attempt.gameInstanceId)
      .then((data) => {
        if (cancelled) return
        const mine = data.attempts.find((a) => a.attemptId === attempt.id)
        setAnswers(mine?.answers ?? [])
      })
      .catch(() => {
        if (!cancelled) setLoadFailed(true)
      })
    return () => {
      cancelled = true
    }
  }, [attempt.gameInstanceId, attempt.id])

  if (loadFailed) {
    return attempt.answersJson ? (
      <p className="text-xs text-slate-400">{attempt.answersJson}</p>
    ) : (
      <p className="text-xs italic text-slate-400">Couldn't load this student's answers.</p>
    )
  }
  if (!answers) return <Spinner />
  if (answers.length === 0) return <p className="text-xs italic text-slate-400">No answers submitted.</p>

  return (
    <div className="space-y-3">
      {answers.map((b) => (
        <div key={b.questionId} className="space-y-1.5 rounded-xl border border-white/10 p-3">
          <p className="text-sm font-semibold">{b.order}. {b.prompt}</p>
          <AnswerView gameType={attempt.gameType} breakdown={b} />
        </div>
      ))}
    </div>
  )
}

function ReviewCard({ attempt, onDone }: { attempt: AttemptAdmin; onDone: () => void }) {
  const [score, setScore] = useState(String(attempt.score))
  const [feedback, setFeedback] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async () => {
    setBusy(true)
    setError(null)
    try {
      await api(`/api/admin/attempts/${attempt.id}/review`, {
        method: 'POST',
        body: { score: Number(score), feedback: feedback || null },
      })
      onDone()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Review failed.')
      setBusy(false)
    }
  }

  return (
    <Card className="space-y-3">
      <div className="flex items-start justify-between gap-2">
        <div>
          <h2 className="font-bold">{attempt.gameTitle}</h2>
          <p className="text-sm text-slate-600">
            {attempt.studentDisplayName}
            {attempt.studentFirstName && (
              <span className="text-slate-400"> — {attempt.studentFirstName} {attempt.studentLastName}</span>
            )}
          </p>
        </div>
        <Badge value={attempt.status} />
      </div>
      <p className="text-sm">
        Auto score: <b>{attempt.score}/{attempt.maxScore}</b> · {attempt.earnedXp} XP awarded so far
      </p>
      <AttemptAnswersView attempt={attempt} />
      <div className="flex flex-col gap-2 sm:flex-row">
        <input
          className={`${inputClass} sm:!w-28`}
          type="number"
          min="0"
          max={attempt.maxScore}
          value={score}
          onChange={(e) => setScore(e.target.value)}
          aria-label={`Final score out of ${attempt.maxScore}`}
        />
        <input
          className={inputClass}
          placeholder="Feedback for the student (optional)"
          value={feedback}
          onChange={(e) => setFeedback(e.target.value)}
          maxLength={2000}
        />
        <Button onClick={submit} disabled={busy} variant="success" className="shrink-0">
          {busy ? 'Saving…' : 'Finalize'}
        </Button>
      </div>
      <ErrorText message={error} />
    </Card>
  )
}

export default function Reviews() {
  const [attempts, setAttempts] = useState<AttemptAdmin[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = () =>
    api<AttemptAdmin[]>('/api/admin/attempts?pendingOnly=true')
      .then(setAttempts)
      .catch((e) => setError(e.message))

  useEffect(() => {
    load()
  }, [])

  if (error) return <ErrorText message={error} />
  if (!attempts) return <Spinner />

  return (
    <div className="space-y-3">
      <h1 className="text-2xl font-bold">📝 Pending reviews</h1>
      {attempts.length === 0 && (
        <Card><p className="text-sm text-slate-500">Nothing to review — all caught up! 🎉</p></Card>
      )}
      {attempts.map((a) => (
        <ReviewCard key={a.id} attempt={a} onDone={load} />
      ))}
    </div>
  )
}

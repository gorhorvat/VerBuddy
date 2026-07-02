import { useEffect, useState } from 'react'
import { api, type AttemptAdmin } from '../../api'
import { Badge, Button, Card, ErrorText, Spinner, inputClass } from '../../components/ui'

function AnswersPreview({ answersJson }: { answersJson: string | null }) {
  if (!answersJson) return null
  try {
    const answers = JSON.parse(answersJson) as { questionId: number; answer: unknown }[]
    return (
      <div className="space-y-1 rounded-xl bg-slate-50 p-3 text-xs">
        {answers.map((a) => (
          <p key={a.questionId} className="font-mono">
            Q{a.questionId}: {JSON.stringify(a.answer)}
          </p>
        ))}
      </div>
    )
  } catch {
    return <p className="text-xs text-slate-400">{answersJson}</p>
  }
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
      <AnswersPreview answersJson={attempt.answersJson} />
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
      <h1 className="text-lg font-bold">📝 Pending reviews</h1>
      {attempts.length === 0 && (
        <Card><p className="text-sm text-slate-500">Nothing to review — all caught up! 🎉</p></Card>
      )}
      {attempts.map((a) => (
        <ReviewCard key={a.id} attempt={a} onDone={load} />
      ))}
    </div>
  )
}

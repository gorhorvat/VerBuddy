import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type AnswerBreakdown, type AttemptAnswers, type AssignmentAnswers as AssignmentAnswersData } from '../../api'
import { Badge, Button, Card, ErrorText, Spinner, assignmentTypeLabels } from '../../components/ui'
import AnswerView from '../../components/AnswerView'

function OverrideControl({
  attemptId,
  breakdown,
  onSaved,
  onError,
}: {
  attemptId: number
  breakdown: AnswerBreakdown
  onSaved: () => void
  onError: (message: string) => void
}) {
  const [points, setPoints] = useState(String(breakdown.finalPoints))
  const [busy, setBusy] = useState(false)
  const dirty = Number(points) !== breakdown.finalPoints

  const save = async () => {
    setBusy(true)
    try {
      await api(`/api/admin/attempts/${attemptId}/override-answer`, {
        method: 'POST',
        body: { questionId: breakdown.questionId, points: Number(points) },
      })
      onSaved()
    } catch (err) {
      onError(err instanceof Error ? err.message : 'Override failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex items-center gap-2 text-xs">
      <span className="text-slate-500">
        auto: <b>{breakdown.autoPoints}</b>/{breakdown.points}
        {breakdown.isOverridden && <span className="ml-1 text-amber-600">(overridden)</span>}
      </span>
      <input
        type="number"
        min={0}
        max={breakdown.points}
        value={points}
        onChange={(e) => setPoints(e.target.value)}
        className="w-16 rounded-lg border border-slate-300 px-2 py-1 text-center text-sm font-semibold"
        aria-label="Awarded points"
      />
      {dirty && (
        <Button variant="success" className="!px-3 !py-1" onClick={save} disabled={busy}>
          {busy ? '…' : 'Save'}
        </Button>
      )}
    </div>
  )
}

function AttemptCard({
  attempt,
  assignmentType,
  onChanged,
  onError,
}: {
  attempt: AttemptAnswers
  assignmentType: AssignmentAnswersData['assignmentType']
  onChanged: () => void
  onError: (message: string) => void
}) {
  const [open, setOpen] = useState(false)

  return (
    <Card className="space-y-3">
      <button type="button" className="flex w-full items-center justify-between gap-2 text-left" onClick={() => setOpen((v) => !v)}>
        <div className="min-w-0">
          <p className="font-bold">
            {attempt.studentDisplayName}
            {attempt.studentFirstName && (
              <span className="ml-2 text-sm font-normal text-slate-400">
                {attempt.studentFirstName} {attempt.studentLastName}
              </span>
            )}
          </p>
          <p className="text-xs text-slate-500">
            {attempt.score}/{attempt.maxScore} pts · {attempt.earnedXp} XP
            {attempt.submittedAtUtc && ` · ${new Date(attempt.submittedAtUtc).toLocaleString()}`}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Badge value={attempt.status} />
          <span className="text-slate-400">{open ? '▲' : '▼'}</span>
        </div>
      </button>

      {open &&
        attempt.answers.map((b) => (
          <div key={b.questionId} className="space-y-2 rounded-xl border border-slate-100 p-3">
            <div className="flex items-start justify-between gap-2">
              <p className="text-sm font-semibold">{b.order}. {b.prompt}</p>
              <OverrideControl attemptId={attempt.attemptId} breakdown={b} onSaved={onChanged} onError={onError} />
            </div>
            <AnswerView assignmentType={assignmentType} breakdown={b} />
          </div>
        ))}
    </Card>
  )
}

/** All students' answers for one assignment, with per-answer point overrides. */
export default function AssignmentAnswers() {
  const { id } = useParams() as { id: string }
  const [data, setData] = useState<AssignmentAnswersData | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = () =>
    api<AssignmentAnswersData>(`/api/admin/assignments/${id}/answers`)
      .then(setData)
      .catch((e) => setError(e.message))

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  if (error && !data) return <ErrorText message={error} />
  if (!data) return <Spinner />

  return (
    <div className="space-y-3">
      <Link to="/teacher/assignments" className="text-sm font-semibold text-indigo-600">← All assignments</Link>
      <h1 className="text-2xl font-bold">{data.title} — answers</h1>
      <p className="text-xs text-slate-500">
        {assignmentTypeLabels[data.assignmentType]} · adjusting points recalculates the student's score and XP immediately.
      </p>
      <ErrorText message={error} />
      {data.attempts.length === 0 && (
        <Card><p className="text-sm text-slate-500">No submitted attempts yet.</p></Card>
      )}
      {data.attempts.map((attempt) => (
        <AttemptCard
          key={attempt.attemptId}
          attempt={attempt}
          assignmentType={data.assignmentType}
          onChanged={load}
          onError={setError}
        />
      ))}
    </div>
  )
}

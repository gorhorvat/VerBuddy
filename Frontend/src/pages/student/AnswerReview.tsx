import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type MyAnswers } from '../../api'
import { Badge, Card, ErrorText, Spinner, gameTypeLabels } from '../../components/ui'
import AnswerView from '../../components/AnswerView'

/** Read-only review of the student's own finalized attempt, with correct answers. */
export default function AnswerReview() {
  const { id } = useParams() as { id: string }
  const [data, setData] = useState<MyAnswers | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api<MyAnswers>(`/api/student/games/${id}/answers`)
      .then(setData)
      .catch((e) => setError(e.message))
  }, [id])

  if (error) {
    return (
      <div className="space-y-3">
        <ErrorText message={error} />
        <Link to="/games" className="text-sm font-semibold text-indigo-600">← Back to games</Link>
      </div>
    )
  }
  if (!data) return <Spinner />

  return (
    <div className="space-y-3">
      <Link to="/games" className="text-sm font-semibold text-indigo-600">← Back to games</Link>
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-lg font-bold">{data.title}</h1>
        <Badge value={data.result.status} />
      </div>
      <p className="text-xs text-slate-500">{gameTypeLabels[data.gameType]}</p>

      <Card className="flex items-center justify-between !py-3">
        <span className="text-sm">
          Your score: <b>{data.result.score}/{data.result.maxScore}</b>
        </span>
        <span className="rounded-full bg-indigo-50 px-3 py-1 text-sm font-bold text-indigo-700">
          +{data.result.earnedXp} XP
        </span>
      </Card>

      {data.result.teacherFeedback && (
        <Card className="!py-3">
          <p className="text-xs font-semibold text-slate-500">Teacher's feedback</p>
          <p className="mt-1 text-sm">{data.result.teacherFeedback}</p>
        </Card>
      )}

      {data.answers.map((b) => (
        <Card key={b.questionId} className="space-y-2">
          <div className="flex items-baseline justify-between gap-2">
            <p className="font-semibold">{b.order}. {b.prompt}</p>
            <span className="shrink-0 text-xs font-bold text-slate-500">
              {b.finalPoints}/{b.points} pt{b.points === 1 ? '' : 's'}
              {b.isOverridden && <span className="ml-1 text-amber-600">(adjusted by teacher)</span>}
            </span>
          </div>
          <AnswerView gameType={data.gameType} breakdown={b} />
        </Card>
      ))}
    </div>
  )
}

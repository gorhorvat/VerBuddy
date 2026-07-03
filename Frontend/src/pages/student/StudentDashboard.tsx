import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type StudentGameSummary } from '../../api'
import { Badge, Button, Card, ErrorText, Spinner, gameTypeLabels } from '../../components/ui'

function formatLimit(seconds: number | null) {
  if (!seconds) return 'Untimed'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `⏱ ${m > 0 ? `${m}m ` : ''}${s > 0 ? `${s}s` : ''}`.trim()
}

const finalizedStatuses = ['Completed', 'PendingReview', 'Invalidated']

function GameCard({ game: g, past }: { game: StudentGameSummary; past: boolean }) {
  const finalized = finalizedStatuses.includes(g.myStatus)

  return (
    <Card className="space-y-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h2 className="font-bold">{g.title}</h2>
          {g.description && <p className="text-sm text-slate-500">{g.description}</p>}
        </div>
        <Badge value={g.myStatus} label={g.myStatus === 'NotStarted' && past ? 'Not played' : undefined} />
      </div>
      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
        <span>{gameTypeLabels[g.gameType]}</span>
        <span>{g.questionCount} question{g.questionCount === 1 ? '' : 's'}</span>
        <span>{formatLimit(g.timeLimitSeconds)}</span>
        <span>⭐ {g.xpReward} XP</span>
      </div>

      {finalized && (
        <div className="rounded-xl bg-slate-50 px-4 py-3 text-sm">
          {g.myStatus === 'Invalidated' ? (
            <span className="text-rose-700">Submitted too late — no score.</span>
          ) : (
            <>
              Score: <b>{g.myScore}/{g.myMaxScore}</b> · earned <b>{g.myEarnedXp} XP</b>
              {g.myStatus === 'PendingReview' && (
                <span className="text-amber-700"> · awaiting teacher review</span>
              )}
            </>
          )}
        </div>
      )}

      {!past && !finalized && (
        <Link to={`/games/${g.id}/play`} className="block">
          <Button className="w-full">
            {g.myStatus === 'InProgress' ? 'Continue' : 'Play now'}
          </Button>
        </Link>
      )}

      {past && finalized && (
        <Link to={`/games/${g.id}/answers`} className="block">
          <Button variant="secondary" className="w-full">Review my answers</Button>
        </Link>
      )}
      {past && !finalized && (
        <p className="text-xs italic text-slate-400">This game has ended.</p>
      )}
    </Card>
  )
}

export default function StudentDashboard() {
  const [games, setGames] = useState<StudentGameSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api<StudentGameSummary[]>('/api/student/games')
      .then(setGames)
      .catch((e) => setError(e.message))
  }, [])

  if (error) return <ErrorText message={error} />
  if (!games) return <Spinner />

  const current = games.filter((g) => g.state === 'Active')
  const past = games.filter((g) => g.state === 'Closed')

  return (
    <div className="space-y-3">
      <h1 className="text-2xl font-bold">🎲 Current games</h1>
      {current.length === 0 && (
        <Card>
          <p className="text-sm text-slate-500">No active games right now. Check back later!</p>
        </Card>
      )}
      {current.map((g) => (
        <GameCard key={g.id} game={g} past={false} />
      ))}

      {past.length > 0 && (
        <>
          <h1 className="pt-4 text-2xl font-bold">📚 Past games</h1>
          {past.map((g) => (
            <GameCard key={g.id} game={g} past />
          ))}
        </>
      )}
    </div>
  )
}

import { useEffect, useState } from 'react'
import { api, type LeaderboardEntry, type Leaderboards } from '../api'
import { useAuth } from '../auth'
import { Card, ErrorText, Spinner } from '../components/ui'

const medals = ['🥇', '🥈', '🥉']

function Board({ entries, myName }: { entries: LeaderboardEntry[]; myName?: string }) {
  if (entries.length === 0)
    return <p className="text-sm text-slate-500">No students on this board yet.</p>

  return (
    <div className="space-y-3">
      {entries.map((e) => {
        const isMe = e.displayName === myName
        return (
          <Card
            key={e.displayName}
            className={`flex items-center gap-3 !py-3 ${isMe ? 'ring-2 ring-indigo-400' : ''}`}
          >
            <span className="w-8 text-center text-lg font-bold">
              {medals[e.rank - 1] ?? e.rank}
            </span>
            <span className="flex-1 truncate font-semibold">
              {e.displayName}
              {isMe && <span className="ml-2 text-xs font-normal text-indigo-600">(you)</span>}
            </span>
            <span className="rounded-full bg-indigo-50 px-3 py-1 font-mono text-sm font-bold text-indigo-700">
              {e.totalXp} XP
            </span>
          </Card>
        )
      })}
    </div>
  )
}

/** Class + global rankings — nicknames and XP only (GDPR-safe by design). */
export default function Leaderboard() {
  const { user } = useAuth()
  const [data, setData] = useState<Leaderboards | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<'class' | 'global'>('global')

  useEffect(() => {
    api<Leaderboards>('/api/leaderboard')
      .then((d) => {
        setData(d)
        if (d.className) setTab('class') // Default to the caller's own class.
      })
      .catch((e) => setError(e.message))
  }, [])

  if (error) return <ErrorText message={error} />
  if (!data) return <Spinner />

  const hasClass = data.className !== null

  return (
    <div className="space-y-3">
      <h1 className="text-lg font-bold">🏆 Leaderboard</h1>

      {hasClass && (
        <div className="flex rounded-xl bg-slate-200 p-1">
          <button
            type="button"
            onClick={() => setTab('class')}
            className={`flex-1 rounded-lg px-3 py-1.5 text-sm font-semibold ${tab === 'class' ? 'bg-indigo-600/15 text-indigo-600' : 'text-slate-500'}`}
          >
            {data.className}
          </button>
          <button
            type="button"
            onClick={() => setTab('global')}
            className={`flex-1 rounded-lg px-3 py-1.5 text-sm font-semibold ${tab === 'global' ? 'bg-indigo-600/15 text-indigo-600' : 'text-slate-500'}`}
          >
            Everyone
          </button>
        </div>
      )}

      <Board
        entries={tab === 'class' && hasClass ? data.classEntries : data.globalEntries}
        myName={user?.displayName}
      />
    </div>
  )
}

import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type StudentAssignmentSummary } from '../../api'
import { Badge, Button, Card, ErrorText, Spinner, assignmentTypeLabels } from '../../components/ui'

function formatLimit(seconds: number | null) {
  if (!seconds) return 'Untimed'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `⏱ ${m > 0 ? `${m}m ` : ''}${s > 0 ? `${s}s` : ''}`.trim()
}

const finalizedStatuses = ['Completed', 'PendingReview', 'Invalidated']

function AssignmentCard({ assignment: g, past }: { assignment: StudentAssignmentSummary; past: boolean }) {
  const finalized = finalizedStatuses.includes(g.myStatus)

  return (
    <Card className="space-y-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h2 className="font-bold">{g.title}</h2>
          {g.description && <p className="text-sm text-slate-500">{g.description}</p>}
        </div>
        <Badge value={g.myStatus} label={g.myStatus === 'NotStarted' && past ? 'Not started' : undefined} />
      </div>
      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
        <span>{assignmentTypeLabels[g.assignmentType]}</span>
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
        <Link to={`/assignments/${g.id}/take`} className="block">
          <Button className="w-full">
            {g.myStatus === 'InProgress' ? 'Continue' : 'Start'}
          </Button>
        </Link>
      )}

      {past && finalized && (
        <Link to={`/assignments/${g.id}/answers`} className="block">
          <Button variant="secondary" className="w-full">Review my answers</Button>
        </Link>
      )}
      {past && !finalized && (
        <p className="text-xs italic text-slate-400">This assignment has ended.</p>
      )}
    </Card>
  )
}

export default function StudentDashboard() {
  const [assignments, setAssignments] = useState<StudentAssignmentSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api<StudentAssignmentSummary[]>('/api/student/assignments')
      .then(setAssignments)
      .catch((e) => setError(e.message))
  }, [])

  if (error) return <ErrorText message={error} />
  if (!assignments) return <Spinner />

  const current = assignments.filter((g) => g.state === 'Active')
  const past = assignments.filter((g) => g.state === 'Closed')

  return (
    <div className="space-y-3">
      <h1 className="text-2xl font-bold">📋 Current assignments</h1>
      {current.length === 0 && (
        <Card>
          <p className="text-sm text-slate-500">No active assignments right now. Check back later!</p>
        </Card>
      )}
      {groupByCategory(current).map((group) => (
        <section key={group.name} className="space-y-3">
          <h2 className="pt-2 text-sm font-bold uppercase tracking-wide text-slate-400">
            📁 {group.name}
          </h2>
          {group.assignments.map((g) => (
            <AssignmentCard key={g.id} assignment={g} past={false} />
          ))}
        </section>
      ))}

      {past.length > 0 && (
        <>
          <h1 className="pt-4 text-2xl font-bold">📚 Past assignments</h1>
          {past.map((g) => (
            <AssignmentCard key={g.id} assignment={g} past />
          ))}
        </>
      )}
    </div>
  )
}

/** Groups assignments by categoryName (alphabetical), with the ungrouped "General" bucket last. */
function groupByCategory(assignments: StudentAssignmentSummary[]) {
  const byCategory = new Map<string, StudentAssignmentSummary[]>()
  for (const g of assignments) {
    const key = g.categoryName ?? 'General'
    byCategory.set(key, [...(byCategory.get(key) ?? []), g])
  }
  const names = [...byCategory.keys()].filter((n) => n !== 'General').sort()
  const result = names.map((name) => ({ name, assignments: byCategory.get(name)! }))
  if (byCategory.has('General')) result.push({ name: 'General', assignments: byCategory.get('General')! })
  return result
}

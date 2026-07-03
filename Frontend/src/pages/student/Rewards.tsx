import { useEffect, useState } from 'react'
import { api, type StudentReward } from '../../api'
import { Button, Card, ErrorText, Spinner } from '../../components/ui'

const statusBadge: Record<string, { className: string; label: string }> = {
  Pending: { className: 'bg-amber-100 text-amber-800', label: 'Pending' },
  Approved: { className: 'bg-emerald-100 text-emerald-800', label: 'Approved' },
  Denied: { className: 'bg-rose-100 text-rose-800', label: 'Denied' },
}

/**
 * Student-facing reward catalogue, ordered by required level (server-side).
 * Locked rewards are greyed out; unlocked ones can be applied for — the
 * teacher then approves or denies the request. A denied request may be
 * re-submitted.
 */
export default function Rewards() {
  const [rewards, setRewards] = useState<StudentReward[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)

  const load = () =>
    api<StudentReward[]>('/api/student/rewards').then(setRewards).catch((e) => setError(e.message))

  useEffect(() => {
    load()
  }, [])

  const apply = async (id: number) => {
    setError(null)
    setBusyId(id)
    try {
      // 400 = level too low, 409 = already pending/approved — both surface below.
      await api(`/api/student/rewards/${id}/apply`, { method: 'POST' })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Applying for the reward failed.')
    } finally {
      setBusyId(null)
    }
  }

  if (error && !rewards) return <ErrorText message={error} />
  if (!rewards) return <Spinner />

  return (
    <div className="space-y-3">
      <h1 className="text-2xl font-bold">🎁 Rewards</h1>
      <ErrorText message={error} />

      {rewards.length === 0 && (
        <Card>
          <p className="text-sm text-slate-500">No rewards yet — check back later!</p>
        </Card>
      )}

      {rewards.map((r) => {
        const status = r.myApplicationStatus ? statusBadge[r.myApplicationStatus] : null
        const canApply = r.unlocked && (r.myApplicationStatus === null || r.myApplicationStatus === 'Denied')
        return (
          <Card key={r.id} className={`space-y-3 !py-4 ${r.unlocked ? '' : 'opacity-50'}`}>
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <h2 className="font-bold">
                  {!r.unlocked && <span className="mr-2" aria-hidden="true">🔒</span>}
                  {r.title}
                </h2>
                {r.description && <p className="text-sm text-slate-500">{r.description}</p>}
              </div>
              <span className="shrink-0 rounded-none border border-indigo-600 px-1.5 py-0.5 text-[10px] font-bold tracking-wide text-indigo-600">
                LV {r.requiredLevel}
              </span>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              {!r.unlocked && (
                <p className="text-xs font-semibold text-slate-400">Unlocks at level {r.requiredLevel}</p>
              )}
              {status && (
                <span className={`inline-block rounded-none px-2.5 py-0.5 text-xs font-semibold ${status.className}`}>
                  {status.label}
                </span>
              )}
              {canApply && (
                <Button
                  className="!px-4 !py-1.5 !text-sm"
                  disabled={busyId === r.id}
                  onClick={() => apply(r.id)}
                >
                  {r.myApplicationStatus === 'Denied' ? 'Apply again' : 'Apply'}
                </Button>
              )}
            </div>
          </Card>
        )
      })}
    </div>
  )
}

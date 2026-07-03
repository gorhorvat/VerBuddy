import { useEffect, useState, type FormEvent } from 'react'
import { api, type Reward, type RewardApplication } from '../../api'
import { Button, Card, ErrorText, Field, Spinner, inputClass } from '../../components/ui'
import ConfirmDialog from '../../components/ConfirmDialog'
import Modal from '../../components/Modal'

const emptyForm = { title: '', description: '', requiredLevel: '1' }

const statusBadge: Record<string, string> = {
  Pending: 'bg-amber-100 text-amber-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  Denied: 'bg-rose-100 text-rose-800',
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })
}

/**
 * Teacher rewards hub: decide pending student applications on top, manage the
 * reward catalogue (create/edit/delete) below.
 */
export default function RewardsAdmin() {
  const [rewards, setRewards] = useState<Reward[] | null>(null)
  const [applications, setApplications] = useState<RewardApplication[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busyIds, setBusyIds] = useState<Set<number>>(new Set())
  const [showDecided, setShowDecided] = useState(false)

  // Create/edit modal state — editId null means "create".
  const [showForm, setShowForm] = useState(false)
  const [editId, setEditId] = useState<number | null>(null)
  const [form, setForm] = useState(emptyForm)
  const [deleteTarget, setDeleteTarget] = useState<Reward | null>(null)
  const [revokeTarget, setRevokeTarget] = useState<RewardApplication | null>(null)

  const load = () =>
    Promise.all([
      api<Reward[]>('/api/admin/rewards').then(setRewards),
      api<RewardApplication[]>('/api/admin/rewards/applications').then(setApplications),
    ]).catch((e) => setError(e.message))

  useEffect(() => {
    load()
  }, [])

  const run = async (action: () => Promise<unknown>) => {
    setError(null)
    try {
      await action()
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'The action failed.')
    }
  }

  const decide = async (id: number, verdict: 'approve' | 'deny') => {
    setBusyIds((prev) => new Set(prev).add(id))
    // 409 = another admin decided it first — the reload shows the outcome.
    await run(() => api(`/api/admin/rewards/applications/${id}/${verdict}`, { method: 'POST' }))
    setBusyIds((prev) => {
      const next = new Set(prev)
      next.delete(id)
      return next
    })
  }

  const openCreate = () => {
    setEditId(null)
    setForm(emptyForm)
    setShowForm(true)
  }

  const openEdit = (r: Reward) => {
    setEditId(r.id)
    setForm({ title: r.title, description: r.description ?? '', requiredLevel: String(r.requiredLevel) })
    setShowForm(true)
  }

  const saveReward = (e: FormEvent) => {
    e.preventDefault()
    const body = {
      title: form.title.trim(),
      description: form.description.trim() || null,
      requiredLevel: Number(form.requiredLevel),
    }
    run(async () => {
      if (editId !== null) {
        await api(`/api/admin/rewards/${editId}`, { method: 'PUT', body })
      } else {
        await api('/api/admin/rewards', { method: 'POST', body })
      }
      setShowForm(false)
    })
  }

  const revoke = (id: number) => run(() => api(`/api/admin/rewards/applications/${id}/revoke`, { method: 'POST' }))

  if (error && (!rewards || !applications)) return <ErrorText message={error} />
  if (!rewards || !applications) return <Spinner />

  const pending = applications.filter((a) => a.status === 'Pending')
  const approved = applications.filter((a) => a.status === 'Approved')
  const decided = applications.filter((a) => a.status !== 'Pending')

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">🎁 Rewards</h1>
        <Button onClick={openCreate}>+ Add reward</Button>
      </div>
      <ErrorText message={error} />

      {/* ── Applications ─────────────────────────────────────────────────── */}
      <h2 className="pt-2 text-sm font-bold uppercase tracking-wide text-slate-400">
        Pending requests {pending.length > 0 && `(${pending.length})`}
      </h2>
      {pending.length === 0 && (
        <Card className="!py-3">
          <p className="text-sm text-slate-500">No pending reward requests.</p>
        </Card>
      )}
      {pending.map((a) => {
        const busy = busyIds.has(a.id)
        return (
          <Card key={a.id} className="flex flex-wrap items-center justify-between gap-3 !py-3">
            <div className="min-w-0">
              <p className="font-semibold">
                {a.studentDisplayName}
                <span className="font-normal text-slate-500"> wants </span>
                {a.rewardTitle}
              </p>
              <p className="text-xs text-slate-500">
                Requires level {a.requiredLevel} · requested {formatDate(a.createdAtUtc)}
              </p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button
                variant="success"
                className="!px-4 !py-1.5 !text-sm"
                disabled={busy}
                onClick={() => decide(a.id, 'approve')}
              >
                Approve
              </Button>
              <Button
                variant="danger"
                className="!px-4 !py-1.5 !text-sm"
                disabled={busy}
                onClick={() => decide(a.id, 'deny')}
              >
                Deny
              </Button>
            </div>
          </Card>
        )
      })}

      {decided.length > 0 && (
        <>
          <button
            type="button"
            onClick={() => setShowDecided((v) => !v)}
            className="text-sm font-semibold text-slate-500 hover:text-slate-700"
          >
            {showDecided ? '▾' : '▸'} Decided requests ({decided.length})
          </button>
          {showDecided &&
            decided.map((a) => (
              <Card key={a.id} className="flex flex-wrap items-center justify-between gap-3 !py-3 opacity-70">
                <div className="min-w-0">
                  <p className="font-semibold">
                    {a.studentDisplayName}
                    <span className="font-normal text-slate-500"> · </span>
                    {a.rewardTitle}
                  </p>
                  <p className="text-xs text-slate-500">
                    Requested {formatDate(a.createdAtUtc)}
                    {a.decidedAtUtc && ` · decided ${formatDate(a.decidedAtUtc)}`}
                  </p>
                </div>
                <span className={`inline-block rounded-none px-2.5 py-0.5 text-xs font-semibold ${statusBadge[a.status]}`}>
                  {a.status}
                </span>
              </Card>
            ))}
        </>
      )}

      {/* ── Approved rewards ─────────────────────────────────────────────── */}
      <h2 className="pt-2 text-sm font-bold uppercase tracking-wide text-slate-400">
        Approved rewards {approved.length > 0 && `(${approved.length})`}
      </h2>
      {approved.length === 0 && (
        <Card className="!py-3">
          <p className="text-sm text-slate-500">No approved rewards yet.</p>
        </Card>
      )}
      {approved.map((a) => {
        const busy = busyIds.has(a.id)
        return (
          <Card key={a.id} className="flex flex-wrap items-center justify-between gap-3 !py-3">
            <div className="min-w-0">
              <p className="font-semibold">
                {a.studentDisplayName}
                <span className="font-normal text-slate-500"> · </span>
                {a.rewardTitle}
              </p>
              <p className="text-xs text-slate-500">
                {a.decidedAtUtc ? `Approved ${formatDate(a.decidedAtUtc)}` : 'Approved'}
              </p>
            </div>
            <Button
              variant="danger"
              className="!px-4 !py-1.5 !text-sm"
              disabled={busy}
              onClick={() => setRevokeTarget(a)}
            >
              Revoke
            </Button>
          </Card>
        )
      })}

      {/* ── Reward catalogue ─────────────────────────────────────────────── */}
      <h2 className="pt-2 text-sm font-bold uppercase tracking-wide text-slate-400">Manage rewards</h2>
      {rewards.length === 0 && (
        <Card>
          <p className="text-sm text-slate-500">
            No rewards yet — click "+ Add reward" to create the first one.
          </p>
        </Card>
      )}
      {rewards.map((r) => (
        <Card key={r.id} className="flex flex-wrap items-center justify-between gap-3 !py-3">
          <div className="min-w-0">
            <p className="font-semibold">
              {r.title}
              <span className="ml-2 inline-block rounded-none border border-indigo-600 px-1.5 py-0.5 align-middle text-[10px] font-bold tracking-wide text-indigo-600">
                LV {r.requiredLevel}
              </span>
            </p>
            {r.description && <p className="text-sm text-slate-500">{r.description}</p>}
          </div>
          <div className="flex shrink-0 gap-2">
            <Button variant="secondary" className="!px-4 !py-1.5 !text-sm" onClick={() => openEdit(r)}>
              ✏️ Edit
            </Button>
            <Button variant="danger" className="!px-4 !py-1.5 !text-sm" onClick={() => setDeleteTarget(r)}>
              🗑 Delete
            </Button>
          </div>
        </Card>
      ))}

      <Modal
        open={showForm}
        title={editId !== null ? 'Edit reward' : 'Add reward'}
        onClose={() => setShowForm(false)}
      >
        <form onSubmit={saveReward} className="space-y-4">
          <Field label="Title *">
            <input
              className={inputClass}
              placeholder="e.g. Homework-free day"
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              required
              maxLength={200}
            />
          </Field>
          <Field label="Description">
            <input
              className={inputClass}
              placeholder="Optional"
              value={form.description}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              maxLength={1000}
            />
          </Field>
          <Field label="Required level *">
            <input
              className={inputClass}
              type="number"
              min="0"
              max="99"
              value={form.requiredLevel}
              onChange={(e) => setForm((f) => ({ ...f, requiredLevel: e.target.value }))}
              required
            />
          </Field>
          <div className="flex justify-end gap-3">
            <Button type="button" variant="secondary" onClick={() => setShowForm(false)}>
              Cancel
            </Button>
            <Button type="submit">{editId !== null ? 'Save changes' : 'Create reward'}</Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete reward?"
        message={`"${deleteTarget?.title}" and its application history will be permanently deleted.`}
        confirmLabel="Delete reward"
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => {
          const id = deleteTarget!.id
          setDeleteTarget(null)
          run(() => api(`/api/admin/rewards/${id}`, { method: 'DELETE' }))
        }}
      />

      <ConfirmDialog
        open={revokeTarget !== null}
        title="Revoke reward?"
        message={`${revokeTarget?.studentDisplayName} will lose access to "${revokeTarget?.rewardTitle}".`}
        confirmLabel="Revoke reward"
        onCancel={() => setRevokeTarget(null)}
        onConfirm={() => {
          const id = revokeTarget!.id
          setRevokeTarget(null)
          revoke(id)
        }}
      />
    </div>
  )
}

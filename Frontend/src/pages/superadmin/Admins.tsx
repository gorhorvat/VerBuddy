import { useEffect, useState, type FormEvent } from 'react'
import { api, type AdminAccount } from '../../api'
import { Button, Card, ErrorText, Spinner, inputClass } from '../../components/ui'
import ConfirmDialog from '../../components/ConfirmDialog'
import Modal from '../../components/Modal'

const emptyForm = { username: '', firstName: '', lastName: '', email: '', displayName: '' }

/**
 * SuperAdmin-only roster of admin (teacher) accounts. Same lifecycle as
 * students: create (no password) → Activate emails a temporary password →
 * the admin changes it on first login.
 */
export default function Admins() {
  const [admins, setAdmins] = useState<AdminAccount[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<AdminAccount | null>(null)
  const [editId, setEditId] = useState<string | null>(null)
  const [busyIds, setBusyIds] = useState<Set<string>>(new Set())

  const [form, setForm] = useState(emptyForm)
  const [editForm, setEditForm] = useState(emptyForm)

  const load = () =>
    api<AdminAccount[]>('/api/superadmin/admins').then(setAdmins).catch((e) => setError(e.message))

  useEffect(() => {
    load()
  }, [])

  const run = async (action: () => Promise<unknown>, successNotice?: string) => {
    setError(null)
    setNotice(null)
    try {
      await action()
      if (successNotice) setNotice(successNotice)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'The action failed.')
    }
  }

  const runFor = async (id: string, action: () => Promise<unknown>, successNotice?: string) => {
    setBusyIds((prev) => new Set(prev).add(id))
    await run(action, successNotice)
    setBusyIds((prev) => {
      const next = new Set(prev)
      next.delete(id)
      return next
    })
  }

  const set =
    (setter: typeof setForm) => (key: keyof typeof emptyForm) => (e: { target: { value: string } }) =>
      setter((f) => ({ ...f, [key]: e.target.value }))
  const setCreate = set(setForm)
  const setEdit = set(setEditForm)

  const create = (e: FormEvent) => {
    e.preventDefault()
    run(async () => {
      await api('/api/superadmin/admins', {
        method: 'POST',
        body: {
          username: form.username,
          firstName: form.firstName || null,
          lastName: form.lastName || null,
          email: form.email || null,
          displayName: form.displayName || null,
        },
      })
      setForm(emptyForm)
      setShowCreate(false)
    }, 'Admin created. Use "Activate" to email their first-login credentials.')
  }

  const startEdit = (a: AdminAccount) => {
    setEditId(a.id)
    setEditForm({
      username: a.username,
      firstName: a.firstName ?? '',
      lastName: a.lastName ?? '',
      email: a.email ?? '',
      displayName: a.displayName,
    })
  }

  const saveEdit = (e: FormEvent) => {
    e.preventDefault()
    const id = editId!
    run(async () => {
      await api(`/api/superadmin/admins/${id}`, {
        method: 'PUT',
        body: {
          firstName: editForm.firstName || null,
          lastName: editForm.lastName || null,
          email: editForm.email || null,
          displayName: editForm.displayName || null,
        },
      })
      setEditId(null)
    }, 'Admin updated.')
  }

  if (!admins) return <Spinner />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">🛡️ Admins</h1>
        <Button onClick={() => setShowCreate(true)}>+ Add admin</Button>
      </div>

      <ErrorText message={error} />
      {notice && <p className="rounded-lg bg-emerald-50 px-4 py-3 text-base text-emerald-800">{notice}</p>}

      <Modal open={showCreate} title="Add admin" onClose={() => setShowCreate(false)}>
        <form onSubmit={create} className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <input className={inputClass} placeholder="Login username *" value={form.username} onChange={setCreate('username')} required minLength={3} maxLength={50} />
            <input className={inputClass} type="email" placeholder="Email (needed for activation)" value={form.email} onChange={setCreate('email')} />
            <input className={inputClass} placeholder="First name" value={form.firstName} onChange={setCreate('firstName')} />
            <input className={inputClass} placeholder="Last name" value={form.lastName} onChange={setCreate('lastName')} />
            <input className={inputClass} placeholder="Nickname (empty = auto-generate)" value={form.displayName} onChange={setCreate('displayName')} maxLength={32} />
          </div>
          <p className="text-sm text-slate-500">
            No password needed — activating the account emails a temporary password,
            which must be replaced on first login.
          </p>
          <Button type="submit" className="w-full">Create admin</Button>
        </form>
      </Modal>

      {admins.map((a) => {
        const busy = busyIds.has(a.id)
        return (
          <Card key={a.id} className={`space-y-2 !py-3 ${!a.isActive ? 'opacity-60' : ''}`}>
            <div className="flex items-center gap-3">
              <div className="min-w-0 flex-1">
                <p className="font-semibold">
                  {a.firstName || a.lastName ? `${a.firstName ?? ''} ${a.lastName ?? ''}`.trim() : a.username}
                  <span className="ml-2 text-sm font-normal text-indigo-600">"{a.displayName}"</span>
                </p>
                <p className="truncate text-xs text-slate-500">
                  @{a.username}
                  {a.email && ` · ${a.email}`}
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2">
                {!a.isActive ? (
                  <span className="rounded-full bg-rose-100 px-2.5 py-0.5 text-xs font-semibold text-rose-800">Deactivated</span>
                ) : a.activatedAt === null ? (
                  <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800">Not activated</span>
                ) : a.mustChangePassword ? (
                  <span className="rounded-full bg-sky-100 px-2.5 py-0.5 text-xs font-semibold text-sky-800">Awaiting first login</span>
                ) : (
                  <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-semibold text-emerald-800">Active</span>
                )}
                {a.isActive && (
                  <>
                    <Button
                      variant="secondary"
                      className="!px-4 !py-1.5 !text-sm"
                      disabled={busy}
                      onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/activate`, { method: 'POST' }), `Activation email sent to ${a.email}.`)}
                    >
                      ✉ {a.activatedAt ? 'Re-send activation' : 'Activate'}
                    </Button>
                    <Button
                      variant="secondary"
                      className="!px-4 !py-1.5 !text-sm"
                      disabled={busy}
                      onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/reset-password`, { method: 'POST' }), `Password reset link sent to ${a.email}.`)}
                    >
                      🔑 Reset password
                    </Button>
                    <Button
                      variant="secondary"
                      className="!px-4 !py-1.5 !text-sm"
                      disabled={busy}
                      onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/deactivate`, { method: 'POST' }))}
                    >
                      ⏸ Deactivate
                    </Button>
                  </>
                )}
                {!a.isActive && (
                  <Button
                    variant="secondary"
                    className="!px-4 !py-1.5 !text-sm"
                    disabled={busy}
                    onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/reactivate`, { method: 'POST' }))}
                  >
                    ▶ Reactivate
                  </Button>
                )}
                <Button variant="secondary" className="!px-4 !py-1.5 !text-sm" disabled={busy} onClick={() => startEdit(a)}>
                  ✏️ Edit
                </Button>
                <Button variant="danger" className="!px-4 !py-1.5 !text-sm" disabled={busy} onClick={() => setDeleteTarget(a)}>
                  🗑 Delete
                </Button>
              </div>
          </Card>
        )
      })}

      <Modal open={editId !== null} title="Edit admin" onClose={() => setEditId(null)}>
        <form onSubmit={saveEdit} className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <input className={inputClass} placeholder="First name" value={editForm.firstName} onChange={setEdit('firstName')} />
            <input className={inputClass} placeholder="Last name" value={editForm.lastName} onChange={setEdit('lastName')} />
            <input className={inputClass} type="email" placeholder="Email" value={editForm.email} onChange={setEdit('email')} />
            <input className={inputClass} placeholder="Nickname" value={editForm.displayName} onChange={setEdit('displayName')} maxLength={32} />
          </div>
          <div className="flex justify-end gap-3">
            <Button type="button" variant="secondary" onClick={() => setEditId(null)}>Cancel</Button>
            <Button type="submit">Save changes</Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete admin?"
        message={`"${deleteTarget?.username}" will be permanently deleted and will no longer be able to sign in.`}
        confirmLabel="Delete admin"
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => {
          const id = deleteTarget!.id
          setDeleteTarget(null)
          run(() => api(`/api/superadmin/admins/${id}`, { method: 'DELETE' }))
        }}
      />
    </div>
  )
}

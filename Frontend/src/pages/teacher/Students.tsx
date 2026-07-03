import { useEffect, useRef, useState, type FormEvent } from 'react'
import {
  api,
  type BulkActivateResult,
  type Category,
  type ImportStudentRow,
  type ImportStudentsResult,
  type StudentAdmin,
} from '../../api'
import { Button, Card, ErrorText, Field, Spinner, inputClass } from '../../components/ui'
import ConfirmDialog from '../../components/ConfirmDialog'
import Modal from '../../components/Modal'

/**
 * Teacher-only roster — the one place in the UI where real names and emails
 * appear. Lifecycle: create (no password) → Activate emails a temporary
 * password → student changes it on first login.
 */
export default function Students() {
  const [students, setStudents] = useState<StudentAdmin[] | null>(null)
  const [categories, setCategories] = useState<Category[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [deleteTarget, setDeleteTarget] = useState<StudentAdmin | null>(null)
  const [editId, setEditId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState({
    firstName: '', lastName: '', email: '', displayName: '', categoryIds: [] as number[],
  })
  const [busyIds, setBusyIds] = useState<Set<string>>(new Set())
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [importCategoryId, setImportCategoryId] = useState('')

  const [form, setForm] = useState({
    username: '', firstName: '', lastName: '', email: '', displayName: '', categoryIds: [] as number[],
  })

  const load = () =>
    Promise.all([
      api<StudentAdmin[]>('/api/admin/students').then(setStudents),
      api<Category[]>('/api/admin/categories').then(setCategories),
    ]).catch((e) => setError(e.message))

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

  const set = (key: 'username' | 'firstName' | 'lastName' | 'email' | 'displayName') => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const setEdit = (key: 'firstName' | 'lastName' | 'email' | 'displayName') => (e: { target: { value: string } }) =>
    setEditForm((f) => ({ ...f, [key]: e.target.value }))

  const toggleFormCategory = (id: number) =>
    setForm((f) => ({
      ...f,
      categoryIds: f.categoryIds.includes(id) ? f.categoryIds.filter((c) => c !== id) : [...f.categoryIds, id],
    }))

  const toggleEditCategory = (id: number) =>
    setEditForm((f) => ({
      ...f,
      categoryIds: f.categoryIds.includes(id) ? f.categoryIds.filter((c) => c !== id) : [...f.categoryIds, id],
    }))

  const startEdit = (s: StudentAdmin) => {
    setEditId(s.id)
    setEditForm({
      firstName: s.firstName ?? '',
      lastName: s.lastName ?? '',
      email: s.email ?? '',
      displayName: s.displayName,
      categoryIds: s.categories.map((c) => c.id),
    })
  }

  const saveEdit = (e: FormEvent) => {
    e.preventDefault()
    const id = editId!
    run(async () => {
      await api(`/api/admin/students/${id}`, {
        method: 'PUT',
        body: {
          firstName: editForm.firstName || null,
          lastName: editForm.lastName || null,
          email: editForm.email || null,
          displayName: editForm.displayName || null,
          categoryIds: editForm.categoryIds.length ? editForm.categoryIds : null,
        },
      })
      setEditId(null)
    }, 'Student updated.')
  }

  const create = (e: FormEvent) => {
    e.preventDefault()
    run(async () => {
      await api('/api/admin/students', {
        method: 'POST',
        body: {
          username: form.username,
          firstName: form.firstName || null,
          lastName: form.lastName || null,
          email: form.email || null,
          displayName: form.displayName || null,
          categoryIds: form.categoryIds.length ? form.categoryIds : null,
        },
      })
      setForm({ username: '', firstName: '', lastName: '', email: '', displayName: '', categoryIds: [] })
      setShowCreate(false)
    }, 'Student created. Use "Activate" to email their first-login credentials.')
  }

  /** CSV: optional header, columns username,firstName,lastName,email[,displayName]. */
  const importCsv = async (file: File) => {
    const text = await file.text()
    const lines = text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean)
    if (lines.length === 0) {
      setError('The file is empty.')
      return
    }
    if (/username/i.test(lines[0])) lines.shift() // Drop the header row.

    const rows: ImportStudentRow[] = lines.map((line) => {
      const [username, firstName, lastName, email, displayName] = line.split(',').map((c) => c.trim())
      return {
        username: username ?? '',
        firstName: firstName || null,
        lastName: lastName || null,
        email: email || null,
        displayName: displayName || null,
      }
    })

    run(async () => {
      const result = await api<ImportStudentsResult>('/api/admin/students/import', {
        method: 'POST',
        body: { rows, categoryId: importCategoryId ? Number(importCategoryId) : null },
      })
      setNotice(
        `Imported ${result.created.length} student${result.created.length === 1 ? '' : 's'}.` +
          (result.errors.length ? ` Skipped: ${result.errors.join(' | ')}` : ''),
      )
    })
  }

  const toggleSelected = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const activateSelected = () =>
    run(async () => {
      const result = await api<BulkActivateResult>('/api/admin/students/activate-bulk', {
        method: 'POST',
        body: { studentIds: [...selected] },
      })
      setSelected(new Set())
      setNotice(
        `Activation email sent to ${result.activated} student${result.activated === 1 ? '' : 's'}.` +
          (result.errors.length ? ` Errors: ${result.errors.join(' | ')}` : ''),
      )
    })

  if (!students) return <Spinner />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">👥 Students</h1>
        <div className="flex gap-3">
          <Button variant="secondary" onClick={() => fileInputRef.current?.click()}>⬆ Import CSV</Button>
          <Button onClick={() => setShowCreate(true)}>+ Add student</Button>
        </div>
      </div>
      <input
        ref={fileInputRef}
        type="file"
        accept=".csv,text/csv,text/plain"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0]
          if (file) importCsv(file)
          e.target.value = ''
        }}
      />
      <div className="flex items-center gap-2 text-xs text-slate-500">
        <span>CSV columns: username, first name, last name, email, nickname (optional). Import into:</span>
        <select className="rounded-lg border border-slate-300 px-2 py-1" value={importCategoryId} onChange={(e) => setImportCategoryId(e.target.value)}>
          <option value="">No class</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </select>
      </div>

      <ErrorText message={error} />
      {notice && <p className="rounded-lg bg-emerald-50 px-4 py-3 text-base text-emerald-800">{notice}</p>}

      <Modal open={showCreate} title="Add student" onClose={() => setShowCreate(false)}>
        <form onSubmit={create} className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="Login username *">
              <input className={inputClass} value={form.username} onChange={set('username')} required minLength={3} maxLength={50} />
            </Field>
            <Field label="Email">
              <input className={inputClass} type="email" placeholder="Needed for activation" value={form.email} onChange={set('email')} />
            </Field>
            <Field label="First name">
              <input className={inputClass} value={form.firstName} onChange={set('firstName')} />
            </Field>
            <Field label="Last name">
              <input className={inputClass} value={form.lastName} onChange={set('lastName')} />
            </Field>
            <Field label="Nickname">
              <input className={inputClass} placeholder="Empty = auto-generate" value={form.displayName} onChange={set('displayName')} maxLength={32} />
            </Field>
            <div>
              <span className="mb-1.5 block text-sm font-semibold text-slate-500">Classes</span>
              <div className="max-h-40 space-y-1 overflow-y-auto rounded-lg border border-white/20 bg-white/[0.04] p-2">
                {categories.length === 0 && <p className="px-2 py-1 text-xs text-slate-500">No classes yet.</p>}
                {categories.map((c) => (
                  <label key={c.id} className="flex cursor-pointer items-center gap-2 px-2 py-1 text-sm">
                    <input
                      type="checkbox"
                      className="h-4 w-4 accent-indigo-600"
                      checked={form.categoryIds.includes(c.id)}
                      onChange={() => toggleFormCategory(c.id)}
                    />
                    {c.name}
                  </label>
                ))}
              </div>
            </div>
          </div>
          <p className="text-sm text-slate-500">
            No password needed — activating the account emails the student a temporary
            password, which they must replace on first login. Real name and email stay
            private to you; classmates only ever see the nickname.
          </p>
          <Button type="submit" className="w-full">Create account</Button>
        </form>
      </Modal>

      {selected.size > 0 && (
        <div className="sticky top-14 z-10 flex items-center justify-between rounded-xl border border-indigo-600/60 bg-indigo-600/15 px-4 py-2.5 text-indigo-600 shadow backdrop-blur">
          <span className="text-sm font-semibold">{selected.size} selected</span>
          <div className="flex gap-2">
            <Button variant="secondary" className="!py-1.5" onClick={() => setSelected(new Set())}>Clear</Button>
            <Button variant="success" className="!py-1.5" onClick={activateSelected}>✉ Activate selected</Button>
          </div>
        </div>
      )}

      {students.map((s) => {
        const busy = busyIds.has(s.id)
        return (
          <Card key={s.id} className={`space-y-2 !py-3 ${!s.isActive ? 'opacity-60' : ''}`}>
            <div className="flex items-center gap-3">
              <input
                type="checkbox"
                className="h-4 w-4 accent-indigo-600"
                checked={selected.has(s.id)}
                onChange={() => toggleSelected(s.id)}
                aria-label={`Select ${s.username}`}
              />
              <div className="min-w-0 flex-1">
                <p className="font-semibold">
                  {s.firstName || s.lastName ? `${s.firstName ?? ''} ${s.lastName ?? ''}`.trim() : s.username}
                  <span className="ml-2 text-sm font-normal text-indigo-600">"{s.displayName}"</span>
                </p>
                <p className="truncate text-xs text-slate-500">
                  @{s.username}
                  {s.email && ` · ${s.email}`}
                  {s.categories.length > 0 && ` · 📁 ${s.categories.map((c) => c.name).join(', ')}`}
                </p>
              </div>
              <span className="shrink-0 rounded-none bg-indigo-50 px-3 py-1 font-mono text-sm font-bold text-indigo-700">
                {s.totalXp} XP
              </span>
            </div>
            <div className="flex flex-wrap items-center gap-2 pl-7">
              {!s.isActive ? (
                <span className="inline-flex items-center rounded-none border border-transparent bg-rose-100 px-4 py-1.5 text-sm font-semibold text-rose-800">Deactivated</span>
              ) : s.activatedAt === null ? (
                <span className="inline-flex items-center rounded-none border border-transparent bg-amber-100 px-4 py-1.5 text-sm font-semibold text-amber-800">Not activated</span>
              ) : s.mustChangePassword ? (
                <span className="inline-flex items-center rounded-none border border-transparent bg-sky-100 px-4 py-1.5 text-sm font-semibold text-sky-800">Awaiting first login</span>
              ) : (
                <span className="inline-flex items-center rounded-none border border-transparent bg-emerald-100 px-4 py-1.5 text-sm font-semibold text-emerald-800">Active</span>
              )}
              {s.isActive && (
                <>
                  <Button
                    variant="secondary"
                    className="!px-4 !py-1.5 !text-sm"
                    disabled={busy}
                    onClick={() => runFor(s.id, () => api(`/api/admin/students/${s.id}/activate`, { method: 'POST' }), `Activation email sent to ${s.email}.`)}
                  >
                    ✉ {s.activatedAt ? 'Re-send activation' : 'Activate'}
                  </Button>
                  <Button
                    variant="secondary"
                    className="!px-4 !py-1.5 !text-sm"
                    disabled={busy}
                    onClick={() => runFor(s.id, () => api(`/api/admin/students/${s.id}/reset-password`, { method: 'POST' }), `Password reset link sent to ${s.email}.`)}
                  >
                    🔑 Reset password
                  </Button>
                  <Button
                    variant="secondary"
                    className="!px-4 !py-1.5 !text-sm"
                    disabled={busy}
                    onClick={() => runFor(s.id, () => api(`/api/admin/students/${s.id}/deactivate`, { method: 'POST' }))}
                  >
                    ⏸ Deactivate
                  </Button>
                </>
              )}
              {!s.isActive && (
                <Button
                  variant="secondary"
                  className="!px-4 !py-1.5 !text-sm"
                  disabled={busy}
                  onClick={() => runFor(s.id, () => api(`/api/admin/students/${s.id}/reactivate`, { method: 'POST' }))}
                >
                  ▶ Reactivate
                </Button>
              )}
              <Button variant="secondary" className="!px-4 !py-1.5 !text-sm" disabled={busy} onClick={() => startEdit(s)}>
                ✏️ Edit
              </Button>
              <Button
                variant="danger"
                className="!px-4 !py-1.5 !text-sm"
                disabled={busy}
                onClick={() => setDeleteTarget(s)}
              >
                🗑 Delete
              </Button>
            </div>
          </Card>
        )
      })}

      <Modal open={editId !== null} title="Edit student" onClose={() => setEditId(null)}>
        <form onSubmit={saveEdit} className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="First name">
              <input className={inputClass} value={editForm.firstName} onChange={setEdit('firstName')} />
            </Field>
            <Field label="Last name">
              <input className={inputClass} value={editForm.lastName} onChange={setEdit('lastName')} />
            </Field>
            <Field label="Email">
              <input className={inputClass} type="email" value={editForm.email} onChange={setEdit('email')} />
            </Field>
            <Field label="Nickname">
              <input className={inputClass} value={editForm.displayName} onChange={setEdit('displayName')} maxLength={32} />
            </Field>
            <div>
              <span className="mb-1.5 block text-sm font-semibold text-slate-500">Classes</span>
              <div className="max-h-40 space-y-1 overflow-y-auto rounded-lg border border-white/20 bg-white/[0.04] p-2">
                {categories.length === 0 && <p className="px-2 py-1 text-xs text-slate-500">No classes yet.</p>}
                {categories.map((c) => (
                  <label key={c.id} className="flex cursor-pointer items-center gap-2 px-2 py-1 text-sm">
                    <input
                      type="checkbox"
                      className="h-4 w-4 accent-indigo-600"
                      checked={editForm.categoryIds.includes(c.id)}
                      onChange={() => toggleEditCategory(c.id)}
                    />
                    {c.name}
                  </label>
                ))}
              </div>
            </div>
          </div>
          <div className="flex justify-end gap-3">
            <Button type="button" variant="secondary" onClick={() => setEditId(null)}>Cancel</Button>
            <Button type="submit">Save changes</Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete student?"
        message={`"${deleteTarget?.username}" will be permanently deleted. This only works for accounts without recorded attempts — students with history should be deactivated instead.`}
        confirmLabel="Delete student"
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => {
          const id = deleteTarget!.id
          setDeleteTarget(null)
          run(() => api(`/api/admin/students/${id}`, { method: 'DELETE' }))
        }}
      />
    </div>
  )
}

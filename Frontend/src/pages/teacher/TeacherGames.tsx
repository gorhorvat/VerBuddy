import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, type Category, type GameDetail, type GameSummary, type GameType } from '../../api'
import { Badge, Button, Card, ErrorText, Field, Spinner, gameTypeLabels, inputClass } from '../../components/ui'
import AvatarStack from '../../components/AvatarStack'
import ConfirmDialog from '../../components/ConfirmDialog'
import Modal from '../../components/Modal'

export default function TeacherGames() {
  const navigate = useNavigate()
  const [games, setGames] = useState<GameSummary[] | null>(null)
  const [categories, setCategories] = useState<Category[]>([])
  const [error, setError] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [showCategories, setShowCategories] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<GameSummary | null>(null)

  // Create form state
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [gameType, setGameType] = useState<GameType>('SingleChoice')
  const [timeLimit, setTimeLimit] = useState('')
  const [xpReward, setXpReward] = useState('100')
  const [requireFeedback, setRequireFeedback] = useState(false)
  const [categoryId, setCategoryId] = useState('')
  const [newCategoryName, setNewCategoryName] = useState('')

  const load = () =>
    Promise.all([
      api<GameSummary[]>('/api/admin/games').then(setGames),
      api<Category[]>('/api/admin/categories').then(setCategories),
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

  const create = (e: FormEvent) => {
    e.preventDefault()
    run(async () => {
      await api('/api/admin/games', {
        method: 'POST',
        body: {
          title,
          description: description || null,
          gameType,
          timeLimitSeconds: timeLimit ? Number(timeLimit) : null,
          xpReward: Number(xpReward),
          requireFeedback,
          categoryId: categoryId ? Number(categoryId) : null,
        },
      })
      setTitle('')
      setDescription('')
      setTimeLimit('')
      setRequireFeedback(false)
      setShowCreate(false)
    })
  }

  const useAsTemplate = async (g: GameSummary) => {
    setError(null)
    try {
      const copy = await api<GameDetail>(`/api/admin/games/${g.id}/duplicate`, { method: 'POST' })
      navigate(`/teacher/games/${copy.id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'The action failed.')
    }
  }

  const addCategory = (e: FormEvent) => {
    e.preventDefault()
    const name = newCategoryName.trim()
    if (!name) return
    run(() => api('/api/admin/categories', { method: 'POST', body: { name } }))
    setNewCategoryName('')
  }

  // Games grouped: each category (alphabetical) plus a trailing General group.
  const groups = useMemo(() => {
    if (!games) return []
    const byCategory = new Map<number | null, GameSummary[]>()
    for (const g of games) {
      const key = g.categoryId
      byCategory.set(key, [...(byCategory.get(key) ?? []), g])
    }
    const result: { name: string; games: GameSummary[] }[] = categories
      .filter((c) => byCategory.has(c.id))
      .map((c) => ({ name: c.name, games: byCategory.get(c.id)! }))
    if (byCategory.has(null)) result.push({ name: 'General', games: byCategory.get(null)! })
    return result
  }, [games, categories])

  if (!games) return <Spinner />

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">🎲 Games</h1>
        <div className="flex gap-3">
          <Button variant="secondary" onClick={() => setShowCategories(true)}>
            📁 Categories
          </Button>
          <Button onClick={() => setShowCreate(true)}>+ New game</Button>
        </div>
      </div>
      <ErrorText message={error} />

      <Modal open={showCategories} title="Categories (classes)" onClose={() => setShowCategories(false)}>
        <div className="space-y-4">
          {categories.map((c) => (
            <div key={c.id} className="flex items-center justify-between gap-2 rounded-xl bg-slate-50 px-3 py-2">
              <span className="text-sm font-semibold">{c.name}</span>
              <span className="flex-1 text-xs text-slate-400">
                {c.gameCount} game{c.gameCount === 1 ? '' : 's'} · {c.studentCount} student{c.studentCount === 1 ? '' : 's'}
              </span>
              <Button
                variant="danger"
                className="!px-2.5 !py-1"
                onClick={() => run(() => api(`/api/admin/categories/${c.id}`, { method: 'DELETE' }))}
              >
                ✕
              </Button>
            </div>
          ))}
          {categories.length === 0 && (
            <p className="text-xs text-slate-500">No categories yet — games live under "General".</p>
          )}
          <form onSubmit={addCategory} className="flex items-end gap-2">
            <Field label="New category" className="flex-1">
              <input className={inputClass} placeholder="e.g. 5th Grade A" value={newCategoryName} onChange={(e) => setNewCategoryName(e.target.value)} maxLength={100} />
            </Field>
            <Button type="submit" variant="secondary" className="shrink-0">Add</Button>
          </form>
          <p className="text-sm text-slate-400">
            Deleting a category never deletes its games or students — they move back to General/unassigned.
          </p>
        </div>
      </Modal>

      <Modal open={showCreate} title="New game" onClose={() => setShowCreate(false)}>
        <form onSubmit={create} className="space-y-4">
            <Field label="Title *">
              <input className={inputClass} placeholder="e.g. Irregular verbs quiz" value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={200} />
            </Field>
            <Field label="Description">
              <input className={inputClass} placeholder="Optional" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={1000} />
            </Field>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label="Game type">
                <select className={inputClass} value={gameType} onChange={(e) => setGameType(e.target.value as GameType)}>
                  {Object.entries(gameTypeLabels).map(([value, label]) => (
                    <option key={value} value={value}>{label}</option>
                  ))}
                </select>
              </Field>
              <Field label="Category (class)">
                <select className={inputClass} value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
                  <option value="">General</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </Field>
              <Field label="Time limit (seconds)">
                <input className={inputClass} type="number" min="0" max="14400" placeholder="Empty = untimed" value={timeLimit} onChange={(e) => setTimeLimit(e.target.value)} />
              </Field>
              <Field label="XP reward *">
                <input className={inputClass} type="number" min="0" max="10000" value={xpReward} onChange={(e) => setXpReward(e.target.value)} required />
              </Field>
            </div>
            <label className="flex cursor-pointer items-start gap-3 rounded-xl bg-slate-50 px-4 py-3">
              <input
                type="checkbox"
                className="mt-0.5 h-4 w-4 accent-indigo-600"
                checked={requireFeedback}
                onChange={(e) => setRequireFeedback(e.target.checked)}
              />
              <span className="text-sm">
                <span className="font-semibold">Require feedback</span>
                <span className="block text-xs text-slate-500">
                  Attempts wait for your manual review instead of being auto-graded.
                </span>
              </span>
            </label>
            <Button type="submit" className="w-full">Create draft</Button>
          </form>
      </Modal>

      {games.length === 0 && (
        <Card>
          <p className="text-base text-slate-500">
            No games yet — click "+ New game" to create your first one.
          </p>
        </Card>
      )}

      {groups.map((group) => (
        <section key={group.name} className="space-y-3">
          <h2 className="pt-2 text-sm font-bold uppercase tracking-wide text-slate-400">
            📁 {group.name}
          </h2>
          {group.games.map((g) => (
            <Card key={g.id} className="space-y-3">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <h3 className="font-bold">{g.title}</h3>
                  <p className="text-xs text-slate-500">
                    {gameTypeLabels[g.gameType]} · {g.questionCount} question{g.questionCount === 1 ? '' : 's'} ·{' '}
                    {g.timeLimitSeconds ? `${g.timeLimitSeconds}s limit` : 'untimed'} · ⭐ {g.xpReward} XP
                    {g.requireFeedback && ' · 📝 manual grading'}
                  </p>
                </div>
                <Badge value={g.state} />
              </div>
              {g.attemptCount > 0 && (
                <div className="flex items-center gap-3">
                  <AvatarStack names={g.attemptDisplayNames} />
                  <span className="text-xs text-slate-500">
                    {g.attemptCount} attempt{g.attemptCount === 1 ? '' : 's'}
                  </span>
                </div>
              )}
              <div className="flex flex-wrap items-center gap-2">
                {g.state !== 'Active' && (
                  <Link to={`/teacher/games/${g.id}`}><Button variant="secondary">Edit</Button></Link>
                )}
                {g.state === 'Draft' && (
                  <Button variant="success" onClick={() => run(() => api(`/api/admin/games/${g.id}/state`, { method: 'POST', body: { state: 'Active' } }))}>Activate</Button>
                )}
                {g.state === 'Active' && (
                  <Button variant="secondary" onClick={() => run(() => api(`/api/admin/games/${g.id}/state`, { method: 'POST', body: { state: 'Closed' } }))}>Close</Button>
                )}
                {g.state === 'Closed' && (
                  <Button variant="secondary" onClick={() => run(() => api(`/api/admin/games/${g.id}/state`, { method: 'POST', body: { state: 'Active' } }))}>Reopen</Button>
                )}
                {g.attemptCount > 0 && (
                  <Link to={`/teacher/games/${g.id}/answers`}><Button variant="secondary">Answers</Button></Link>
                )}
                <Button variant="secondary" onClick={() => useAsTemplate(g)}>Use as template</Button>
                {g.attemptCount === 0 && (
                  <Button variant="danger" onClick={() => setDeleteTarget(g)}>Delete</Button>
                )}
                <select
                  className="ml-auto rounded-lg border border-white/20 bg-white/[0.04] px-2 py-1.5 text-xs"
                  value={g.categoryId ?? ''}
                  onChange={(e) =>
                    run(() => api(`/api/admin/games/${g.id}/category`, {
                      method: 'POST',
                      body: { categoryId: e.target.value ? Number(e.target.value) : null },
                    }))
                  }
                  aria-label="Category"
                >
                  <option value="">General</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>
            </Card>
          ))}
        </section>
      ))}

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete game?"
        message={`"${deleteTarget?.title}" and all of its questions will be permanently deleted. This cannot be undone.`}
        confirmLabel="Delete game"
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => {
          const id = deleteTarget!.id
          setDeleteTarget(null)
          run(() => api(`/api/admin/games/${id}`, { method: 'DELETE' }))
        }}
      />
    </div>
  )
}

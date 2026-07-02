import { useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type GameDetail, type GameType, type QuestionAdmin } from '../../api'
import { Badge, Button, Card, ErrorText, Spinner, gameTypeLabels, inputClass } from '../../components/ui'
import ConfirmDialog from '../../components/ConfirmDialog'

/**
 * Question builder/editor. Teachers author content in friendly text formats
 * converted into the per-game-type JSON the backend validates. Editing is
 * available in Draft and Closed states — only Active games are frozen.
 */
export default function GameEditor() {
  const { id } = useParams() as { id: string }
  const [game, setGame] = useState<GameDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<QuestionAdmin | null>(null)

  // Question form state; editingId != null means we're editing an existing one.
  const [editingId, setEditingId] = useState<number | null>(null)
  const [prompt, setPrompt] = useState('')
  const [points, setPoints] = useState('1')
  const [order, setOrder] = useState<number | null>(null)
  const [choicesText, setChoicesText] = useState('')
  const [correctIndexes, setCorrectIndexes] = useState<Set<number>>(new Set())
  const [template, setTemplate] = useState('')
  const [blankAnswers, setBlankAnswers] = useState<string[]>([])
  const [pairsText, setPairsText] = useState('')

  const load = () =>
    api<GameDetail>(`/api/admin/games/${id}`).then(setGame).catch((e) => setError(e.message))

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  const choices = choicesText.split('\n').map((c) => c.trim()).filter(Boolean)
  const blankCount = template.split('___').length - 1

  const resetForm = () => {
    setEditingId(null)
    setPrompt('')
    setPoints('1')
    setOrder(null)
    setChoicesText('')
    setCorrectIndexes(new Set())
    setTemplate('')
    setBlankAnswers([])
    setPairsText('')
  }

  /** Loads an existing question's JSON back into the friendly form fields. */
  const startEditing = (q: QuestionAdmin) => {
    if (!game) return
    setEditingId(q.id)
    setPrompt(q.prompt)
    setPoints(String(q.points))
    setOrder(q.order)
    const content = JSON.parse(q.jsonContent)
    switch (game.gameType) {
      case 'SingleChoice':
        setChoicesText((content.choices as string[]).join('\n'))
        setCorrectIndexes(new Set([content.correctIndex as number]))
        break
      case 'MultipleChoice':
        setChoicesText((content.choices as string[]).join('\n'))
        setCorrectIndexes(new Set(content.correctIndexes as number[]))
        break
      case 'FillInTheBlanks':
        setTemplate(content.template as string)
        setBlankAnswers(
          (content.blanks as { acceptedAnswers: string[] }[]).map((b) => b.acceptedAnswers.join(', ')),
        )
        break
      case 'WordMatching':
        setPairsText(
          (content.pairs as { key: string; value: string }[]).map((p) => `${p.key} = ${p.value}`).join('\n'),
        )
        break
    }
    window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' })
  }

  const buildJsonContent = (type: GameType): string => {
    switch (type) {
      case 'SingleChoice':
        return JSON.stringify({ choices, correctIndex: [...correctIndexes][0] ?? -1 })
      case 'MultipleChoice':
        return JSON.stringify({ choices, correctIndexes: [...correctIndexes].sort((a, b) => a - b) })
      case 'FillInTheBlanks':
        return JSON.stringify({
          template,
          blanks: Array.from({ length: blankCount }, (_, i) => ({
            acceptedAnswers: (blankAnswers[i] ?? '').split(',').map((a) => a.trim()).filter(Boolean),
            caseSensitive: false,
          })),
        })
      case 'WordMatching':
        return JSON.stringify({
          instructions: prompt || 'Match the pairs.',
          shuffleRightColumn: true,
          pairs: pairsText
            .split('\n')
            .map((line) => line.split('='))
            .filter((parts) => parts.length === 2)
            .map(([key, value]) => ({ key: key.trim(), value: value.trim() })),
        })
    }
  }

  const saveQuestion = async (e: FormEvent) => {
    e.preventDefault()
    if (!game) return
    setError(null)
    const body = {
      prompt,
      order: order ?? game.questions.length + 1,
      points: Number(points),
      jsonContent: buildJsonContent(game.gameType),
    }
    try {
      if (editingId !== null) {
        await api(`/api/admin/games/${id}/questions/${editingId}`, { method: 'PUT', body })
      } else {
        await api(`/api/admin/games/${id}/questions`, { method: 'POST', body })
      }
      resetForm()
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Saving the question failed.')
    }
  }

  const deleteQuestion = async (questionId: number) => {
    setError(null)
    try {
      await api(`/api/admin/games/${id}/questions/${questionId}`, { method: 'DELETE' })
      if (editingId === questionId) resetForm()
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed.')
    }
  }

  const toggleCorrect = (i: number, single: boolean) => {
    setCorrectIndexes((prev) => {
      if (single) return new Set([i])
      const next = new Set(prev)
      if (next.has(i)) next.delete(i)
      else next.add(i)
      return next
    })
  }

  if (error && !game) return <ErrorText message={error} />
  if (!game) return <Spinner />

  const editable = game.state !== 'Active'
  const isChoice = game.gameType === 'SingleChoice' || game.gameType === 'MultipleChoice'

  return (
    <div className="space-y-3">
      <Link to="/teacher/games" className="text-sm font-semibold text-indigo-600">← All games</Link>
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-lg font-bold">{game.title}</h1>
        <Badge value={game.state} />
      </div>
      <p className="text-xs text-slate-500">
        {gameTypeLabels[game.gameType]} · 📁 {game.categoryName ?? 'General'}
        {game.attemptCount > 0 && ` · ${game.attemptCount} recorded attempt${game.attemptCount === 1 ? '' : 's'}`}
      </p>
      <ErrorText message={error} />

      {game.attemptCount > 0 && editable && (
        <p className="rounded-xl bg-amber-50 px-4 py-3 text-xs text-amber-800">
          Students already attempted this game. Content changes won't re-grade
          existing attempts — use the Answers view to adjust points instead.
        </p>
      )}

      {game.questions.map((q) => (
        <Card key={q.id} className={`flex items-start justify-between gap-3 !py-3 ${editingId === q.id ? 'ring-2 ring-indigo-400' : ''}`}>
          <div className="min-w-0">
            <p className="font-semibold">{q.order}. {q.prompt}</p>
            <p className="truncate text-xs text-slate-400">{q.jsonContent}</p>
          </div>
          {editable && (
            <div className="flex shrink-0 gap-2">
              <Button variant="secondary" className="!px-3 !py-1.5" onClick={() => startEditing(q)}>Edit</Button>
              <Button variant="danger" className="!px-3 !py-1.5" onClick={() => setDeleteTarget(q)}>✕</Button>
            </div>
          )}
        </Card>
      ))}

      {!editable && (
        <p className="rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-500">
          This game is Active. Close it to edit questions or settings.
        </p>
      )}

      {editable && (
        <Card>
          <form onSubmit={saveQuestion} className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="font-bold">{editingId !== null ? `Edit question ${order}` : 'Add question'}</h2>
              {editingId !== null && (
                <button type="button" onClick={resetForm} className="text-sm font-semibold text-slate-500 hover:underline">
                  Cancel edit
                </button>
              )}
            </div>
            <input className={inputClass} placeholder="Prompt / instructions" value={prompt} onChange={(e) => setPrompt(e.target.value)} required maxLength={2000} />

            {isChoice && (
              <>
                <textarea className={inputClass} rows={4} placeholder={'Choices — one per line'} value={choicesText} onChange={(e) => setChoicesText(e.target.value)} required />
                {choices.length > 0 && (
                  <div className="space-y-1">
                    <p className="text-xs font-semibold text-slate-500">
                      Tick the correct answer{game.gameType === 'MultipleChoice' ? 's' : ''}:
                    </p>
                    {choices.map((c, i) => (
                      <label key={i} className="flex items-center gap-2 text-sm">
                        <input
                          type={game.gameType === 'SingleChoice' ? 'radio' : 'checkbox'}
                          name="correct"
                          checked={correctIndexes.has(i)}
                          onChange={() => toggleCorrect(i, game.gameType === 'SingleChoice')}
                        />
                        {c}
                      </label>
                    ))}
                  </div>
                )}
              </>
            )}

            {game.gameType === 'FillInTheBlanks' && (
              <>
                <textarea className={inputClass} rows={3} placeholder={'Sentence with ___ for each blank, e.g.\nShe ___ to school yesterday.'} value={template} onChange={(e) => setTemplate(e.target.value)} required />
                {Array.from({ length: blankCount }, (_, i) => (
                  <input
                    key={i}
                    className={inputClass}
                    placeholder={`Accepted answers for blank ${i + 1} (comma-separated)`}
                    value={blankAnswers[i] ?? ''}
                    onChange={(e) =>
                      setBlankAnswers((prev) => {
                        const next = [...prev]
                        next[i] = e.target.value
                        return next
                      })
                    }
                    required
                  />
                ))}
              </>
            )}

            {game.gameType === 'WordMatching' && (
              <textarea className={inputClass} rows={5} placeholder={'One pair per line:\nword = definition\ngenerous = willing to give more than expected'} value={pairsText} onChange={(e) => setPairsText(e.target.value)} required />
            )}

            <div className="flex items-center gap-3">
              <label className="text-sm text-slate-600">Points:</label>
              <input className={`${inputClass} !w-24`} type="number" min="1" max="1000" value={points} onChange={(e) => setPoints(e.target.value)} required />
              <Button type="submit" className="flex-1">
                {editingId !== null ? 'Save changes' : 'Add question'}
              </Button>
            </div>
          </form>
        </Card>
      )}

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete question?"
        message={`Question ${deleteTarget?.order} ("${deleteTarget?.prompt}") will be permanently deleted.`}
        confirmLabel="Delete question"
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => {
          const qid = deleteTarget!.id
          setDeleteTarget(null)
          deleteQuestion(qid)
        }}
      />
    </div>
  )
}

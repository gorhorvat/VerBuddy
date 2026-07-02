// Answer input components, one per game type. Each parses the sanitized
// (answer-key-free) jsonContent from the backend and reports an answer object
// in exactly the shape the grading service expects.

interface InputProps {
  jsonContent: string
  value: unknown
  onChange: (answer: unknown) => void
}

const choiceBase =
  'flex w-full items-center gap-3 rounded-xl border px-4 py-3 text-left text-sm font-medium transition-colors'
const choiceIdle = 'border-slate-300 bg-white hover:border-indigo-400'
const choiceSelected = 'border-indigo-600 bg-indigo-50 text-indigo-900'

/** Answer: { selectedIndex: number } */
export function SingleChoiceInput({ jsonContent, value, onChange }: InputProps) {
  const { choices } = JSON.parse(jsonContent) as { choices: string[] }
  const selected = (value as { selectedIndex?: number } | undefined)?.selectedIndex

  return (
    <div className="space-y-2">
      {choices.map((choice, i) => (
        <button
          key={i}
          type="button"
          onClick={() => onChange({ selectedIndex: i })}
          className={`${choiceBase} ${selected === i ? choiceSelected : choiceIdle}`}
        >
          <span
            className={`h-4 w-4 shrink-0 rounded-full border-2 ${selected === i ? 'border-indigo-600 bg-indigo-600' : 'border-slate-400'}`}
          />
          {choice}
        </button>
      ))}
    </div>
  )
}

/** Answer: { selectedIndexes: number[] } */
export function MultipleChoiceInput({ jsonContent, value, onChange }: InputProps) {
  const { choices, correctCount } = JSON.parse(jsonContent) as {
    choices: string[]
    correctCount: number
  }
  const selected = new Set((value as { selectedIndexes?: number[] } | undefined)?.selectedIndexes ?? [])

  const toggle = (i: number) => {
    const next = new Set(selected)
    if (next.has(i)) next.delete(i)
    else next.add(i)
    onChange({ selectedIndexes: [...next].sort((a, b) => a - b) })
  }

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-slate-500">Select {correctCount}:</p>
      {choices.map((choice, i) => (
        <button
          key={i}
          type="button"
          onClick={() => toggle(i)}
          className={`${choiceBase} ${selected.has(i) ? choiceSelected : choiceIdle}`}
        >
          <span
            className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border-2 text-[10px] text-white ${selected.has(i) ? 'border-indigo-600 bg-indigo-600' : 'border-slate-400'}`}
          >
            {selected.has(i) && '✓'}
          </span>
          {choice}
        </button>
      ))}
    </div>
  )
}

/** Answer: { answers: string[] } — inputs rendered inline inside the template. */
export function FillInTheBlanksInput({ jsonContent, value, onChange }: InputProps) {
  const { template, blankCount } = JSON.parse(jsonContent) as {
    template: string
    blankCount: number
  }
  const answers = (value as { answers?: string[] } | undefined)?.answers ??
    Array.from({ length: blankCount }, () => '')
  const parts = template.split('___')

  const setAnswer = (i: number, text: string) => {
    const next = [...answers]
    next[i] = text
    onChange({ answers: next })
  }

  return (
    <p className="text-base leading-9">
      {parts.map((part, i) => (
        <span key={i}>
          {part}
          {i < parts.length - 1 && (
            <input
              className="mx-1 inline-block w-28 rounded-lg border border-slate-300 px-2 py-1 text-center text-sm font-semibold focus:border-indigo-500 focus:outline-none"
              value={answers[i] ?? ''}
              onChange={(e) => setAnswer(i, e.target.value)}
              placeholder={`blank ${i + 1}`}
            />
          )}
        </span>
      ))}
    </p>
  )
}

/** Answer: { matches: Record<key, value> } — a value picker per key. */
export function WordMatchingInput({ jsonContent, value, onChange }: InputProps) {
  const { instructions, keys, values } = JSON.parse(jsonContent) as {
    instructions: string
    keys: string[]
    values: string[]
  }
  const matches = (value as { matches?: Record<string, string> } | undefined)?.matches ?? {}
  const used = new Set(Object.values(matches))

  const setMatch = (key: string, val: string) => {
    const next = { ...matches }
    if (val === '') delete next[key]
    else next[key] = val
    onChange({ matches: next })
  }

  return (
    <div className="space-y-3">
      <p className="text-xs font-semibold text-slate-500">{instructions}</p>
      {keys.map((key) => (
        <div key={key} className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-3">
          <span className="w-32 shrink-0 rounded-lg bg-indigo-50 px-3 py-2 text-sm font-bold text-indigo-900">
            {key}
          </span>
          <select
            className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none"
            value={matches[key] ?? ''}
            onChange={(e) => setMatch(key, e.target.value)}
          >
            <option value="">— choose a match —</option>
            {values.map((v) => (
              <option key={v} value={v} disabled={used.has(v) && matches[key] !== v}>
                {v}
              </option>
            ))}
          </select>
        </div>
      ))}
    </div>
  )
}

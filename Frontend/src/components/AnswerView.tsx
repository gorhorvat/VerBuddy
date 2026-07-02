import type { AnswerBreakdown, GameType } from '../api'

// Renders one graded answer: the student's response against the correct one.
// contentJson here is the FULL content including the answer key, so this
// component is only mounted in teacher views and finalized student reviews.

const rowBase = 'flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm'
const correctRow = 'bg-emerald-50 text-emerald-900'
const wrongRow = 'bg-rose-50 text-rose-900'
const neutralRow = 'bg-slate-50 text-slate-600'

function SingleChoiceAnswer({ b }: { b: AnswerBreakdown }) {
  const { choices, correctIndex } = JSON.parse(b.contentJson) as {
    choices: string[]
    correctIndex: number
  }
  const selected = (b.answer as { selectedIndex?: number } | null)?.selectedIndex

  return (
    <div className="space-y-1">
      {choices.map((choice, i) => {
        const isCorrect = i === correctIndex
        const isSelected = i === selected
        return (
          <div key={i} className={`${rowBase} ${isCorrect ? correctRow : isSelected ? wrongRow : neutralRow}`}>
            <span className="w-5">{isCorrect ? '✓' : isSelected ? '✗' : ''}</span>
            <span className="flex-1">{choice}</span>
            {isSelected && <span className="text-xs font-semibold">student's pick</span>}
          </div>
        )
      })}
      {selected === undefined && <p className="text-xs italic text-slate-400">No answer given.</p>}
    </div>
  )
}

function MultipleChoiceAnswer({ b }: { b: AnswerBreakdown }) {
  const { choices, correctIndexes } = JSON.parse(b.contentJson) as {
    choices: string[]
    correctIndexes: number[]
  }
  const selected = new Set((b.answer as { selectedIndexes?: number[] } | null)?.selectedIndexes ?? [])
  const correct = new Set(correctIndexes)

  return (
    <div className="space-y-1">
      {choices.map((choice, i) => {
        const isCorrect = correct.has(i)
        const isSelected = selected.has(i)
        return (
          <div key={i} className={`${rowBase} ${isCorrect ? correctRow : isSelected ? wrongRow : neutralRow}`}>
            <span className="w-5">{isCorrect ? '✓' : isSelected ? '✗' : ''}</span>
            <span className="flex-1">{choice}</span>
            {isSelected && <span className="text-xs font-semibold">selected</span>}
          </div>
        )
      })}
    </div>
  )
}

function FillInTheBlanksAnswer({ b }: { b: AnswerBreakdown }) {
  const { template, blanks } = JSON.parse(b.contentJson) as {
    template: string
    blanks: { acceptedAnswers: string[]; caseSensitive: boolean }[]
  }
  const given = (b.answer as { answers?: string[] } | null)?.answers ?? []
  const parts = template.split('___')

  const isBlankCorrect = (i: number) => {
    const candidate = (given[i] ?? '').trim()
    return blanks[i]?.acceptedAnswers.some((a) =>
      blanks[i].caseSensitive
        ? a.trim() === candidate
        : a.trim().toLowerCase() === candidate.toLowerCase(),
    )
  }

  return (
    <div className="space-y-2">
      <p className="text-sm leading-8">
        {parts.map((part, i) => (
          <span key={i}>
            {part}
            {i < parts.length - 1 && (
              <span
                className={`mx-1 rounded-lg px-2 py-1 font-semibold ${isBlankCorrect(i) ? 'bg-emerald-100 text-emerald-900' : 'bg-rose-100 text-rose-900'}`}
              >
                {(given[i] ?? '').trim() || '—'}
              </span>
            )}
          </span>
        ))}
      </p>
      {blanks.some((_, i) => !isBlankCorrect(i)) && (
        <p className="text-xs text-slate-500">
          Accepted:{' '}
          {blanks.map((blank, i) => (
            <span key={i} className="mr-3">
              blank {i + 1}: <b>{blank.acceptedAnswers.join(' / ')}</b>
            </span>
          ))}
        </p>
      )}
    </div>
  )
}

function WordMatchingAnswer({ b }: { b: AnswerBreakdown }) {
  const { pairs } = JSON.parse(b.contentJson) as { pairs: { key: string; value: string }[] }
  const matches = (b.answer as { matches?: Record<string, string> } | null)?.matches ?? {}

  return (
    <div className="space-y-1">
      {pairs.map((pair) => {
        const given = matches[pair.key]?.trim()
        const isCorrect = given === pair.value.trim()
        return (
          <div key={pair.key} className={`${rowBase} ${isCorrect ? correctRow : wrongRow}`}>
            <span className="w-5">{isCorrect ? '✓' : '✗'}</span>
            <span className="font-semibold">{pair.key}</span>
            <span className="flex-1">→ {given || '—'}</span>
            {!isCorrect && <span className="text-xs">correct: {pair.value}</span>}
          </div>
        )
      })}
    </div>
  )
}

export default function AnswerView({ gameType, breakdown }: { gameType: GameType; breakdown: AnswerBreakdown }) {
  switch (gameType) {
    case 'SingleChoice':
      return <SingleChoiceAnswer b={breakdown} />
    case 'MultipleChoice':
      return <MultipleChoiceAnswer b={breakdown} />
    case 'FillInTheBlanks':
      return <FillInTheBlanksAnswer b={breakdown} />
    case 'WordMatching':
      return <WordMatchingAnswer b={breakdown} />
  }
}

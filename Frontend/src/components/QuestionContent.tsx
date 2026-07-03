import type { GameType } from '../api'

// Renders a question's jsonContent (the FULL content including the answer
// key) in a human-readable form. Used in the teacher's question list
// (GameEditor) where only the question shape matters — no student answer.

const rowBase = 'flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm'
const correctRow = 'bg-emerald-50 text-emerald-900'
const neutralRow = 'bg-white/5 text-slate-300'

function ChoicesContent({
  choices,
  correct,
}: {
  choices: string[]
  correct: Set<number>
}) {
  return (
    <div className="space-y-1">
      {choices.map((choice, i) => (
        <div key={i} className={`${rowBase} ${correct.has(i) ? correctRow : neutralRow}`}>
          <span className="w-5">{correct.has(i) ? '✓' : ''}</span>
          <span className="flex-1">{choice}</span>
        </div>
      ))}
    </div>
  )
}

function FillInTheBlanksContent({
  template,
  blanks,
}: {
  template: string
  blanks: { acceptedAnswers: string[] }[]
}) {
  const parts = template.split('___')
  return (
    <div className="space-y-2">
      <p className="text-sm leading-8">
        {parts.map((part, i) => (
          <span key={i}>
            {part}
            {i < parts.length - 1 && (
              <span className="mx-1 rounded-lg bg-indigo-600/10 px-2 py-1 font-semibold text-indigo-600">
                _____
              </span>
            )}
          </span>
        ))}
      </p>
      {blanks.length > 0 && (
        <p className="text-xs text-slate-400">
          Accepted:{' '}
          {blanks.map((blank, i) => (
            <span key={i} className="mr-3">
              blank {i + 1}: <b className="text-slate-300">{blank.acceptedAnswers.join(' / ') || '—'}</b>
            </span>
          ))}
        </p>
      )}
    </div>
  )
}

function WordMatchingContentView({ pairs }: { pairs: { key: string; value: string }[] }) {
  return (
    <div className="space-y-1">
      {pairs.map((pair, i) => (
        <div key={i} className={`${rowBase} ${neutralRow}`}>
          <span className="font-semibold text-slate-100">{pair.key}</span>
          <span className="text-slate-400">→</span>
          <span className="flex-1">{pair.value}</span>
        </div>
      ))}
    </div>
  )
}

/** Human-readable rendering of a question's jsonContent, per game type. */
export default function QuestionContent({ gameType, jsonContent }: { gameType: GameType; jsonContent: string }) {
  try {
    const content = JSON.parse(jsonContent) as Record<string, unknown>
    switch (gameType) {
      case 'SingleChoice':
        return (
          <ChoicesContent
            choices={content.choices as string[]}
            correct={new Set([content.correctIndex as number])}
          />
        )
      case 'MultipleChoice':
        return (
          <ChoicesContent
            choices={content.choices as string[]}
            correct={new Set(content.correctIndexes as number[])}
          />
        )
      case 'FillInTheBlanks':
        return (
          <FillInTheBlanksContent
            template={content.template as string}
            blanks={content.blanks as { acceptedAnswers: string[] }[]}
          />
        )
      case 'WordMatching':
        return <WordMatchingContentView pairs={content.pairs as { key: string; value: string }[]} />
      default:
        return <p className="text-xs text-slate-400">{jsonContent}</p>
    }
  } catch {
    return <p className="text-xs text-slate-400">{jsonContent}</p>
  }
}

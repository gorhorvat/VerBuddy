import { Button } from './ui'

/**
 * Celebratory popup shown when a submitted game pushes the student past a
 * level threshold. Rendered on top of the result screen; deliberately has no
 * outside-click dismiss — the Continue button is the only way out.
 */
export default function LevelUpModal({ level, onClose }: { level: number; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 px-4 backdrop-blur-sm">
      <div
        role="dialog"
        aria-modal="true"
        className="w-full max-w-sm rounded-2xl border border-indigo-600/50 bg-[#111111] p-8 text-center shadow-xl"
      >
        <p className="text-6xl" aria-hidden="true">🎉</p>
        <h2 className="mt-4 font-display text-2xl font-extrabold tracking-tight">Congratulations!</h2>
        <p className="mt-1 text-sm font-semibold text-slate-500">You reached</p>
        <p className="font-display text-6xl font-extrabold tracking-tight text-indigo-600">
          Level {level}
        </p>
        <Button className="mt-8 w-full" onClick={onClose} autoFocus>
          Continue
        </Button>
      </div>
    </div>
  )
}

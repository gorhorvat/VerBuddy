// Overlapping initial-avatars for students who attempted a game. Shows up to
// `max` avatars; the overflow collapses into a "+N" chip whose hover popup
// lists the remaining names. Hovering any avatar reveals its display name.

const avatarColors = [
  'bg-indigo-500',
  'bg-emerald-500',
  'bg-amber-500',
  'bg-rose-500',
  'bg-sky-500',
  'bg-violet-500',
  'bg-teal-500',
  'bg-orange-500',
]

function colorFor(name: string) {
  let hash = 0
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) | 0
  return avatarColors[Math.abs(hash) % avatarColors.length]
}

function initials(name: string) {
  const caps = name.match(/[A-Z]/g)
  if (caps && caps.length >= 2) return caps[0] + caps[1]
  return name.slice(0, 2).toUpperCase()
}

function Tooltip({ text }: { text: string }) {
  return (
    <div className="pointer-events-none absolute bottom-full left-1/2 z-20 mb-1.5 hidden -translate-x-1/2 whitespace-nowrap rounded-lg bg-slate-800 px-2 py-1 text-xs font-semibold text-white shadow-lg group-hover:block">
      {text}
    </div>
  )
}

export default function AvatarStack({ names, max = 5 }: { names: string[]; max?: number }) {
  if (names.length === 0) return null

  const visible = names.slice(0, max)
  const overflow = names.slice(max)

  return (
    <div className="flex items-center">
      {visible.map((name) => (
        <div key={name} className="group relative -ml-2 first:ml-0">
          <div
            className={`flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold text-white ring-2 ring-white ${colorFor(name)}`}
          >
            {initials(name)}
          </div>
          <Tooltip text={name} />
        </div>
      ))}
      {overflow.length > 0 && (
        <div className="group relative -ml-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-slate-300 text-xs font-bold text-slate-700 ring-2 ring-white">
            +{overflow.length}
          </div>
          <div className="pointer-events-none absolute bottom-full left-1/2 z-20 mb-1.5 hidden max-h-56 -translate-x-1/2 overflow-y-auto rounded-xl bg-slate-800 px-3 py-2 shadow-lg group-hover:block">
            {overflow.map((name) => (
              <p key={name} className="whitespace-nowrap py-0.5 text-xs font-semibold text-white">
                {name}
              </p>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

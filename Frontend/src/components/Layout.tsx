import { useEffect } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth'
import { progressToNext } from '../lib/levels'

/**
 * Mobile-first shell: sticky top bar with the user's pseudonymous identity,
 * and a bottom tab bar on phones that becomes inline top navigation on ≥sm.
 */
export default function Layout() {
  const { user, isAdmin, isSuperAdmin, logout, refreshMe } = useAuth()

  // Keep the header XP/level display fresh — the login snapshot goes stale
  // as soon as the student finishes a game.
  useEffect(() => {
    refreshMe().catch(() => {
      /* header simply keeps the cached values */
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const tabs = isAdmin
    ? [
        { to: '/teacher/games', label: 'Games', icon: '🎲' },
        { to: '/teacher/reviews', label: 'Reviews', icon: '📝' },
        { to: '/teacher/students', label: 'Students', icon: '👥' },
        ...(isSuperAdmin ? [{ to: '/superadmin/admins', label: 'Admins', icon: '🛡️' }] : []),
        { to: '/teacher/rewards', label: 'Rewards', icon: '🎁' },
        { to: '/leaderboard', label: 'Ranks', icon: '🏆' },
      ]
    : [
        { to: '/games', label: 'Games', icon: '🎲' },
        { to: '/rewards', label: 'Rewards', icon: '🎁' },
        { to: '/leaderboard', label: 'Ranks', icon: '🏆' },
      ]

  const progress = !isAdmin && user ? progressToNext(user.totalXp) : null

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `flex flex-1 flex-col items-center gap-1 rounded-lg border px-5 py-2.5 text-sm font-semibold transition-colors sm:flex-initial sm:flex-row sm:gap-2 sm:text-base ${
      isActive
        ? 'border-indigo-600/60 bg-indigo-600/10 text-indigo-600'
        : 'border-transparent text-slate-500 hover:bg-white/5 hover:text-slate-700'
    }`

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-10 border-b border-white/10 bg-[#040404]/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-6 py-4">
          <div className="flex min-w-0 items-center gap-3">
            <img src="/android-chrome-192x192.png" alt="VerBuddy" className="h-10 w-10 shrink-0" />
            <div className="min-w-0">
            <p className="font-display text-xl font-extrabold tracking-tight text-slate-900">
              <span className="text-indigo-600">Ver</span>Buddy
            </p>
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-500">
              <span className="truncate">{user?.displayName}</span>
              {isAdmin && (
                <span className="shrink-0 rounded bg-indigo-600/15 px-2 py-0.5 text-xs uppercase tracking-wide text-indigo-600">
                  {isSuperAdmin ? 'Super Admin' : 'Admin'}
                </span>
              )}
              {progress && (
                <>
                  <span className="shrink-0 rounded-none border border-indigo-600 px-1.5 py-0.5 text-[10px] font-bold tracking-wide text-indigo-600">
                    LV {progress.level}
                  </span>
                  <span className="flex shrink-0 flex-col gap-0.5">
                    <span
                      className="h-1.5 w-24 overflow-hidden rounded-none bg-white/10 sm:w-32"
                      title={`${progress.current.toLocaleString()} / ${progress.needed.toLocaleString()} XP`}
                    >
                      <span className="block h-full bg-indigo-600" style={{ width: `${progress.pct}%` }} />
                    </span>
                    <span className="text-[10px] font-medium tabular-nums text-slate-500">
                      {progress.current.toLocaleString()} / {progress.needed.toLocaleString()} XP
                    </span>
                  </span>
                </>
              )}
            </div>
            </div>
          </div>
          <nav className="hidden items-center gap-2 sm:flex">
            {tabs.map((t) => (
              <NavLink key={t.to} to={t.to} className={linkClass}>
                <span>{t.icon}</span>
                {t.label}
              </NavLink>
            ))}
          </nav>
          <button
            onClick={logout}
            className="rounded-lg px-4 py-2 text-base font-semibold text-slate-500 hover:bg-white/5 hover:text-slate-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
          >
            Log out
          </button>
        </div>
      </header>

      <main className="flex-1 px-6 py-6 pb-28 sm:pb-10">
        <div className="mx-auto max-w-6xl">
          <Outlet />
        </div>
      </main>

      {/* Bottom tab bar — phones only */}
      <nav className="fixed inset-x-0 bottom-0 z-10 border-t border-white/10 bg-[#040404]/90 backdrop-blur sm:hidden">
        <div className="mx-auto flex max-w-6xl gap-1 px-3 py-2">
          {tabs.map((t) => (
            <NavLink key={t.to} to={t.to} className={linkClass}>
              <span className="text-base">{t.icon}</span>
              {t.label}
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  )
}

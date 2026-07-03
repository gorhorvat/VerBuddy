import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth'

/**
 * Mobile-first shell: sticky top bar with the user's pseudonymous identity,
 * and a bottom tab bar on phones that becomes inline top navigation on ≥sm.
 */
export default function Layout() {
  const { user, isAdmin, isSuperAdmin, logout } = useAuth()

  const tabs = isAdmin
    ? [
        { to: '/teacher/games', label: 'Games', icon: '🎲' },
        { to: '/teacher/reviews', label: 'Reviews', icon: '📝' },
        { to: '/teacher/students', label: 'Students', icon: '👥' },
        ...(isSuperAdmin ? [{ to: '/superadmin/admins', label: 'Admins', icon: '🛡️' }] : []),
        { to: '/leaderboard', label: 'Ranks', icon: '🏆' },
      ]
    : [
        { to: '/games', label: 'Games', icon: '🎲' },
        { to: '/leaderboard', label: 'Ranks', icon: '🏆' },
      ]

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `flex flex-1 flex-col items-center gap-1 rounded-lg border px-5 py-2.5 text-sm font-semibold transition-colors sm:flex-initial sm:flex-row sm:gap-2 sm:text-base ${
      isActive
        ? 'border-indigo-600/60 bg-indigo-600/10 text-indigo-600'
        : 'border-transparent text-slate-500 hover:bg-white/5 hover:text-slate-700'
    }`

  return (
    <div className="mx-auto flex min-h-dvh max-w-6xl flex-col">
      <header className="sticky top-0 z-10 flex items-center justify-between gap-4 border-b border-white/10 bg-[#040404]/80 px-6 py-4 backdrop-blur">
        <div className="min-w-0">
          <p className="font-display text-xl font-extrabold tracking-tight text-slate-900">
            <span className="text-indigo-600">Ver</span>Buddy
          </p>
          <p className="truncate text-sm font-semibold text-slate-500">
            {user?.displayName}
            {isAdmin && (
              <span className="ml-2 rounded bg-indigo-600/15 px-2 py-0.5 text-xs uppercase tracking-wide text-indigo-600">
                {isSuperAdmin ? 'Super Admin' : 'Admin'}
              </span>
            )}
          </p>
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
      </header>

      <main className="flex-1 px-6 py-6 pb-28 sm:pb-10">
        <Outlet />
      </main>

      {/* Bottom tab bar — phones only */}
      <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto flex max-w-6xl gap-1 border-t border-white/10 bg-[#040404]/90 px-3 py-2 backdrop-blur sm:hidden">
        {tabs.map((t) => (
          <NavLink key={t.to} to={t.to} className={linkClass}>
            <span className="text-base">{t.icon}</span>
            {t.label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}

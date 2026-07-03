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
    `flex flex-1 flex-col items-center gap-0.5 rounded-xl px-3 py-1.5 text-xs font-bold transition-colors sm:flex-initial sm:flex-row sm:gap-2 sm:text-sm ${
      isActive
        ? 'border-2 border-slate-900 bg-indigo-600 text-white shadow-tile-sm'
        : 'border-2 border-transparent text-slate-600 hover:bg-slate-200'
    }`

  return (
    <div className="mx-auto flex min-h-dvh max-w-3xl flex-col">
      <header className="sticky top-0 z-10 flex items-center justify-between gap-3 border-b border-slate-200 bg-white/90 px-4 py-3 backdrop-blur">
        <div className="min-w-0">
          <p className="font-display text-base font-extrabold tracking-tight text-slate-900">
            <span className="wordmark-blank">Ver</span>Buddy
          </p>
          <p className="truncate text-xs font-semibold text-indigo-700">
            {user?.displayName}
            {isAdmin && (
              <span className="ml-1.5 rounded bg-indigo-100 px-1.5 py-0.5 text-[10px] uppercase tracking-wide">
                {isSuperAdmin ? 'Super Admin' : 'Admin'}
              </span>
            )}
          </p>
        </div>
        <nav className="hidden items-center gap-1 sm:flex">
          {tabs.map((t) => (
            <NavLink key={t.to} to={t.to} className={linkClass}>
              <span>{t.icon}</span>
              {t.label}
            </NavLink>
          ))}
        </nav>
        <button
          onClick={logout}
          className="rounded-xl px-3 py-1.5 text-sm font-bold text-slate-500 hover:bg-slate-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
        >
          Log out
        </button>
      </header>

      <main className="flex-1 px-4 py-4 pb-24 sm:pb-6">
        <Outlet />
      </main>

      {/* Bottom tab bar — phones only */}
      <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto flex max-w-3xl gap-1 border-t border-slate-200 bg-white px-3 py-2 sm:hidden">
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

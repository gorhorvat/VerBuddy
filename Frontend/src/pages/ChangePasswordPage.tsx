import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { Button, Card, ErrorText, inputClass } from '../components/ui'

/**
 * Forced first-login password change: rendered instead of the app while
 * user.mustChangePassword is set (the temp password from the activation email
 * is only valid to get here).
 */
export default function ChangePasswordPage() {
  const { user, clearMustChangePassword, logout } = useAuth()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (next !== confirm) {
      setError('The new passwords do not match.')
      return
    }
    setError(null)
    setBusy(true)
    try {
      await api('/api/auth/change-password', {
        method: 'POST',
        body: { currentPassword: current, newPassword: next },
      })
      clearMustChangePassword()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Changing the password failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-dvh items-center justify-center px-4">
      <Card className="w-full max-w-sm">
        <div className="mb-6 text-center">
          <p className="text-4xl">🔑</p>
          <h1 className="mt-2 text-xl font-bold">Choose your own password</h1>
          <p className="text-sm text-slate-500">
            Welcome, {user?.displayName}! Before you start, replace the temporary
            password from your email with one only you know.
          </p>
        </div>
        <form onSubmit={onSubmit} className="space-y-4">
          <input className={inputClass} type="password" placeholder="Temporary password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} required />
          <input className={inputClass} type="password" placeholder="New password (min 8 characters)" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} required minLength={8} />
          <input className={inputClass} type="password" placeholder="Repeat new password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required minLength={8} />
          <ErrorText message={error} />
          <Button type="submit" disabled={busy} className="w-full">
            {busy ? 'Saving…' : 'Set password and continue'}
          </Button>
          <button type="button" onClick={logout} className="w-full text-center text-sm text-slate-500 hover:underline">
            Log out
          </button>
        </form>
      </Card>
    </div>
  )
}

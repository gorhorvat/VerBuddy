import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { Button, Card, ErrorText, inputClass } from '../components/ui'

/** Landing page for the emailed reset link (?user=...&token=...). Public. */
export default function ResetPasswordPage() {
  const [params] = useSearchParams()
  const userId = params.get('user')
  const token = params.get('token')

  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (next !== confirm) {
      setError('The passwords do not match.')
      return
    }
    setError(null)
    setBusy(true)
    try {
      await api('/api/auth/reset-password', {
        method: 'POST',
        body: { userId, token, newPassword: next },
      })
      setDone(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Resetting the password failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-dvh items-center justify-center px-4">
      <Card className="w-full max-w-sm">
        <div className="mb-6 text-center">
          <p className="text-4xl">🔒</p>
          <h1 className="mt-2 text-xl font-bold">Reset your password</h1>
        </div>
        {!userId || !token ? (
          <p className="text-sm text-slate-500">
            This reset link is incomplete. Ask your teacher to send a new one.
          </p>
        ) : done ? (
          <div className="space-y-4 text-center">
            <p className="text-sm text-emerald-700">Your password has been changed. 🎉</p>
            <Link to="/"><Button className="w-full">Go to sign in</Button></Link>
          </div>
        ) : (
          <form onSubmit={onSubmit} className="space-y-4">
            <input className={inputClass} type="password" placeholder="New password (min 8 characters)" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} required minLength={8} />
            <input className={inputClass} type="password" placeholder="Repeat new password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required minLength={8} />
            <ErrorText message={error} />
            <Button type="submit" disabled={busy} className="w-full">
              {busy ? 'Saving…' : 'Set new password'}
            </Button>
          </form>
        )}
      </Card>
    </div>
  )
}

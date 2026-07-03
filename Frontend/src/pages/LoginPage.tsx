import { useState, type FormEvent } from 'react'
import { useAuth } from '../auth'
import { Button, Card, ErrorText, inputClass } from '../components/ui'

export default function LoginPage() {
  const { login } = useAuth()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await login(username.trim(), password)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="relative flex min-h-dvh items-center justify-center overflow-hidden px-4">
      <div
        aria-hidden
        className="pointer-events-none absolute left-1/2 top-1/2 h-[480px] w-[480px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-indigo-600/10 blur-3xl"
      />
      <Card className="relative w-full max-w-sm">
        <div className="mb-6 text-center">
          <div className="mb-4 flex justify-center" aria-hidden>
            <div className="flex h-16 w-16 items-center justify-center border-2 border-indigo-600 bg-indigo-600/10 shadow-glow">
              <span className="font-display text-4xl font-extrabold leading-none text-indigo-600">V</span>
            </div>
          </div>
          <h1 className="text-3xl font-extrabold tracking-tight">
            <span className="text-indigo-600">Ver</span>Buddy
          </h1>
        </div>
        <form onSubmit={onSubmit} className="space-y-4">
          <input
            className={inputClass}
            placeholder="Username"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
          />
          <input
            className={inputClass}
            type="password"
            placeholder="Password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          <ErrorText message={error} />
          <Button type="submit" disabled={busy} className="w-full">
            {busy ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
      </Card>
    </div>
  )
}

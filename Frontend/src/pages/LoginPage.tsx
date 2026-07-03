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
    <div className="flex min-h-dvh items-center justify-center px-4">
      <Card className="w-full max-w-sm">
        <div className="mb-6 text-center">
          <div className="mb-3 flex justify-center gap-1.5" aria-hidden>
            {['W', 'O', 'R', 'D', 'S'].map((letter, i) => (
              <span
                key={letter}
                className={`flex h-9 w-9 items-center justify-center rounded-lg border-2 border-slate-900 font-mono text-sm font-bold shadow-tile-sm ${
                  i === 2 ? 'bg-indigo-600 text-white' : 'bg-white text-slate-900'
                }`}
              >
                {letter}
              </span>
            ))}
          </div>
          <h1 className="text-3xl font-extrabold tracking-tight">
            <span className="wordmark-blank">Ver</span>Buddy
          </h1>
          <p className="mt-1 text-sm font-medium text-slate-500">
            Your word-game buddy for English class
          </p>
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

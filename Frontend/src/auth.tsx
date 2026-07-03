import { createContext, useContext, useState, type ReactNode } from 'react'
import { api, setAuthToken, type AuthResponse } from './api'

export interface AuthUser {
  token: string
  displayName: string
  totalXp: number
  roles: string[]
  mustChangePassword: boolean
}

interface AuthContextValue {
  user: AuthUser | null
  /** True for Admin AND SuperAdmin — gates the admin (teacher) UI. */
  isAdmin: boolean
  isSuperAdmin: boolean
  login: (username: string, password: string) => Promise<AuthUser>
  logout: () => void
  /** Called after a successful first-login password change. */
  clearMustChangePassword: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

const STORAGE_KEY = 'verbuddy.auth'

function loadStoredUser(): AuthUser | null {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    const user = JSON.parse(raw) as AuthUser
    setAuthToken(user.token)
    return user
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(loadStoredUser)

  const login = async (username: string, password: string) => {
    const res = await api<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: { username, password },
    })
    const authUser: AuthUser = {
      token: res.token,
      displayName: res.displayName,
      totalXp: res.totalXp,
      roles: res.roles,
      mustChangePassword: res.mustChangePassword,
    }
    setAuthToken(res.token)
    localStorage.setItem(STORAGE_KEY, JSON.stringify(authUser))
    setUser(authUser)
    return authUser
  }

  const logout = () => {
    setAuthToken(null)
    localStorage.removeItem(STORAGE_KEY)
    setUser(null)
  }

  const clearMustChangePassword = () =>
    setUser((current) => {
      if (!current) return current
      const next = { ...current, mustChangePassword: false }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
      return next
    })

  return (
    <AuthContext.Provider
      value={{
        user,
        isAdmin: user?.roles.some((r) => r === 'Admin' || r === 'SuperAdmin') ?? false,
        isSuperAdmin: user?.roles.includes('SuperAdmin') ?? false,
        login,
        logout,
        clearMustChangePassword,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}

import type { ButtonHTMLAttributes, ReactNode } from 'react'

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`rounded-2xl border border-white/10 bg-white/[0.03] p-4 backdrop-blur-md sm:p-6 ${className}`}>{children}</div>
  )
}

/** Outlined controls on dark glass — SPM Interactive house style. */
const buttonVariants = {
  primary: 'border-indigo-600 text-indigo-600 hover:bg-indigo-600/10 hover:shadow-glow',
  secondary: 'border-white/25 text-slate-700 hover:bg-white/5',
  danger: 'border-rose-600 text-rose-600 hover:bg-rose-600/10',
  success: 'border-emerald-600 text-emerald-600 hover:bg-emerald-600/10',
} as const

export function Button({
  variant = 'primary',
  className = '',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: keyof typeof buttonVariants }) {
  return (
    <button
      className={`rounded-lg border bg-transparent px-4 py-2.5 font-display text-sm font-semibold tracking-wide transition-[background-color,box-shadow,border-color] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 motion-reduce:transition-none disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-transparent disabled:hover:shadow-none ${buttonVariants[variant]} ${className}`}
      {...props}
    />
  )
}

const badgeColors: Record<string, string> = {
  Draft: 'bg-slate-200 text-slate-700',
  Active: 'bg-emerald-100 text-emerald-800',
  Closed: 'bg-slate-300 text-slate-600',
  NotStarted: 'bg-indigo-100 text-indigo-800',
  InProgress: 'bg-amber-100 text-amber-800',
  Completed: 'bg-emerald-100 text-emerald-800',
  PendingReview: 'bg-amber-100 text-amber-800',
  Invalidated: 'bg-rose-100 text-rose-800',
}

export function Badge({ value, label }: { value: string; label?: string }) {
  return (
    <span
      className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${badgeColors[value] ?? 'bg-slate-200 text-slate-700'}`}
    >
      {label ?? value.replace(/([a-z])([A-Z])/g, '$1 $2')}
    </span>
  )
}

export function ErrorText({ message }: { message: string | null }) {
  if (!message) return null
  return <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">{message}</p>
}

export function Spinner() {
  return (
    <div className="flex justify-center py-10">
      <div className="h-8 w-8 animate-spin rounded-full border-4 border-indigo-200 border-t-indigo-600" />
    </div>
  )
}

export const inputClass =
  'w-full rounded-lg border border-white/20 bg-white/[0.04] px-3 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 focus:border-indigo-600 focus:outline-none focus:ring-2 focus:ring-indigo-600/25'

export const gameTypeLabels: Record<string, string> = {
  SingleChoice: 'Single Choice',
  MultipleChoice: 'Multiple Choice',
  FillInTheBlanks: 'Fill in the Blanks',
  WordMatching: 'Word Matching',
}

import type { ButtonHTMLAttributes, ReactNode } from 'react'

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6 ${className}`}>{children}</div>
  )
}

const buttonVariants = {
  primary: 'bg-indigo-600 text-white hover:bg-indigo-500 disabled:bg-indigo-300',
  secondary: 'bg-white text-slate-800 hover:bg-slate-50 disabled:text-slate-400',
  danger: 'bg-rose-600 text-white hover:bg-rose-700 disabled:bg-rose-300',
  success: 'bg-emerald-600 text-white hover:bg-emerald-700 disabled:bg-emerald-300',
} as const

/**
 * Letter-tile button: hard offset shadow that collapses on press, like
 * pushing down a word-game tile. Disabled tiles lie flat.
 */
export function Button({
  variant = 'primary',
  className = '',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: keyof typeof buttonVariants }) {
  return (
    <button
      className={`rounded-xl border-2 border-slate-900 px-4 py-2.5 text-sm font-bold shadow-tile transition-[background-color,transform,box-shadow] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 enabled:active:translate-y-[3px] enabled:active:shadow-none motion-reduce:transition-none disabled:cursor-not-allowed disabled:border-slate-300 disabled:shadow-none ${buttonVariants[variant]} ${className}`}
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
  'w-full rounded-xl border-2 border-slate-300 bg-white px-3 py-2.5 text-sm font-medium focus:border-indigo-600 focus:outline-none focus:ring-2 focus:ring-indigo-200'

export const gameTypeLabels: Record<string, string> = {
  SingleChoice: 'Single Choice',
  MultipleChoice: 'Multiple Choice',
  FillInTheBlanks: 'Fill in the Blanks',
  WordMatching: 'Word Matching',
}

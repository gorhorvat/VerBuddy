/**
 * Client-side mirror of the backend's leveling math.
 *
 * Level 0 starts at 0 XP; each level doubles in cost: reaching level n takes
 * a cumulative 1000 * (2^n - 1) XP (level 1 = 1 000, level 2 = 3 000,
 * level 3 = 7 000, …), capped at level 99. All thresholds fit comfortably in
 * standard JS numbers (2^99 * 1000 ≈ 6.3e32 < Number.MAX_VALUE).
 */

export const MAX_LEVEL = 99

/** Cumulative XP required to REACH the given level. */
export function thresholdFor(level: number): number {
  const clamped = Math.max(0, Math.min(level, MAX_LEVEL))
  return 1000 * (2 ** clamped - 1)
}

/** The level a student with `xp` total XP has reached (0–99). */
export function levelForXp(xp: number): number {
  let level = 0
  while (level < MAX_LEVEL && xp >= thresholdFor(level + 1)) level++
  return level
}

export interface LevelProgress {
  level: number
  /** XP earned within the current level. */
  current: number
  /** XP the current level costs in total (1000 * 2^level). */
  needed: number
  /** Percentage (0–100) of the way to the next level. */
  pct: number
}

export function progressToNext(xp: number): LevelProgress {
  const level = levelForXp(xp)
  const needed = 1000 * 2 ** level
  const current = Math.max(0, xp - thresholdFor(level))
  const pct = level >= MAX_LEVEL ? 100 : Math.max(0, Math.min(100, (current / needed) * 100))
  return { level, current, needed, pct }
}

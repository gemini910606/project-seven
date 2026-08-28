import { badRequest } from './http'

/**
 * Hand-rolled validators. The payloads here are small and fixed, so a schema
 * library would cost more bundle size than it saves. Every one of these throws
 * ApiError(400) rather than returning an error value, so route handlers stay linear.
 */

export function str(value: unknown, field: string, opts: { min?: number; max?: number } = {}): string {
  if (typeof value !== 'string') throw badRequest('invalid_field', `${field} must be a string`)
  const { min = 1, max = 256 } = opts
  if (value.length < min || value.length > max) {
    throw badRequest('invalid_field', `${field} must be ${min}-${max} characters`)
  }
  return value
}

export function int(value: unknown, field: string, opts: { min?: number; max?: number } = {}): number {
  if (typeof value !== 'number' || !Number.isInteger(value)) {
    throw badRequest('invalid_field', `${field} must be an integer`)
  }
  const { min = 0, max = Number.MAX_SAFE_INTEGER } = opts
  if (value < min || value > max) {
    throw badRequest('invalid_field', `${field} must be between ${min} and ${max}`)
  }
  return value
}

export function oneOf<T extends string>(value: unknown, field: string, allowed: readonly T[]): T {
  if (typeof value !== 'string' || !allowed.includes(value as T)) {
    throw badRequest('invalid_field', `${field} must be one of: ${allowed.join(", ")}`)
  }
  return value as T
}

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export function uuid(value: unknown, field: string): string {
  const s = str(value, field, { min: 36, max: 36 })
  if (!UUID_RE.test(s)) throw badRequest('invalid_field', `${field} must be a UUID`)
  return s.toLowerCase()
}

/**
 * Soft-hyphen, zero-width and bidi-control characters. Players use these to pad
 * a name into looking like someone else's on the leaderboard, so they are
 * stripped rather than rejected - a rejection just teaches the next attempt to
 * be subtler. Written as code-point escapes so the source file stays ASCII.
 */
const INVISIBLE_RE = /[\u00AD\u200B-\u200F\u202A-\u202E\u2060-\u2064\u206A-\u206F\uFEFF]/g

export function displayName(value: unknown): string {
  const raw = str(value, 'displayName', { min: 2, max: 24 })
  const cleaned = raw.replace(INVISIBLE_RE, '').replace(/\s+/g, ' ').trim()
  if (cleaned.length < 2) throw badRequest('invalid_field', 'displayName is too short after cleaning')
  return cleaned
}

/** Compares dotted numeric versions. Returns <0, 0 or >0 like a comparator. */
export function compareVersions(a: string, b: string): number {
  const pa = a.split('.').map((n) => Number.parseInt(n, 10) || 0)
  const pb = b.split('.').map((n) => Number.parseInt(n, 10) || 0)
  const len = Math.max(pa.length, pb.length)
  for (let i = 0; i < len; i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0)
    if (diff !== 0) return diff
  }
  return 0
}

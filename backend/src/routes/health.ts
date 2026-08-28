import { Hono } from 'hono'
import type { AppBindings } from '../lib/types'

export const health = new Hono<AppBindings>()

/**
 * Liveness plus a real dependency check. A 200 here means the Worker is up AND
 * D1 answers, which is what you actually want a status page to report.
 */
health.get('/', async (c) => {
  let db: 'ok' | 'error' = 'ok'
  try {
    await c.env.DB.prepare('SELECT 1').first()
  } catch {
    db = 'error'
  }
  return c.json(
    {
      status: db === 'ok' ? 'ok' : 'degraded',
      environment: c.env.ENVIRONMENT,
      checks: { d1: db },
    },
    db === 'ok' ? 200 : 503,
  )
})

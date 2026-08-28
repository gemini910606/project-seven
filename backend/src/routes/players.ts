import { Hono } from 'hono'
import { ApiError, now } from '../lib/http'
import { displayName, uuid } from '../lib/validate'
import type { AppBindings } from '../lib/types'

export const players = new Hono<AppBindings>()

/**
 * Register or rename a player.
 *
 * There is deliberately no password. The client mints a UUID on first launch,
 * stores it locally and treats it as a bearer token. That is the right trade
 * for a single-player game with a vanity leaderboard: nobody loses anything
 * valuable if an id leaks, and it costs the player zero friction. If real
 * accounts are ever needed, put an OAuth provider in front and keep this id as
 * the internal key.
 */
players.post('/', async (c) => {
  const body = await c.req.json().catch(() => {
    throw new ApiError(400, 'invalid_json', 'Body must be JSON')
  })

  const id = uuid((body as Record<string, unknown>).id, 'id')
  const name = displayName((body as Record<string, unknown>).displayName)
  const ts = now()

  await c.env.DB.prepare(
    `INSERT INTO players (id, display_name, created_at, last_seen_at)
     VALUES (?1, ?2, ?3, ?3)
     ON CONFLICT(id) DO UPDATE SET display_name = ?2, last_seen_at = ?3`,
  )
    .bind(id, name, ts)
    .run()

  return c.json({ id, displayName: name })
})

players.get('/:id', async (c) => {
  const id = uuid(c.req.param('id'), 'id')

  const row = await c.env.DB.prepare(
    `SELECT id, display_name AS displayName, created_at AS createdAt,
            total_runs AS totalRuns, best_score AS bestScore
     FROM players WHERE id = ?1 AND banned = 0`,
  )
    .bind(id)
    .first()

  if (!row) throw new ApiError(404, 'not_found', 'No such player')
  return c.json(row)
})

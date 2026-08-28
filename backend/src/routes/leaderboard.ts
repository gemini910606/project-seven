import { Hono } from 'hono'
import { str } from '../lib/validate'
import type { AppBindings } from '../lib/types'

export const leaderboard = new Hono<AppBindings>()

/**
 * Top runs for a mission. Derived from the runs table rather than a maintained
 * scoreboard so that banning a player, or re-running the anti-cheat rules over
 * historical data, corrects the board with no migration.
 *
 * Cached at the edge for 30s: leaderboards are read constantly and are not
 * worth a D1 read per view.
 */
leaderboard.get('/:missionId', async (c) => {
  const missionId = str(c.req.param('missionId'), 'missionId', { max: 64 })
  const limitParam = Number.parseInt(c.req.query('limit') ?? '25', 10)
  const limit = Number.isFinite(limitParam) ? Math.min(Math.max(limitParam, 1), 100) : 25

  const { results } = await c.env.DB.prepare(
    `SELECT p.display_name AS playerName,
            r.score, r.duration_ms AS durationMs, r.kills,
            r.submitted_at AS submittedAt
     FROM runs r
     JOIN players p ON p.id = r.player_id
     WHERE r.mission_id = ?1
       AND r.flags = ''
       AND r.outcome = 'extracted'
       AND p.banned = 0
     ORDER BY r.score DESC, r.duration_ms ASC
     LIMIT ?2`,
  )
    .bind(missionId, limit)
    .all()

  return c.json(
    { missionId, entries: results.map((row, i) => ({ rank: i + 1, ...row })) },
    200,
    { 'Cache-Control': 'public, max-age=30' },
  )
})

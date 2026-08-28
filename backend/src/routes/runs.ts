import { Hono } from 'hono'
import { ApiError, now } from '../lib/http'
import { int, oneOf, str, uuid, compareVersions } from '../lib/validate'
import { runSignaturePayload, verify } from '../lib/crypto'
import { flagRun } from '../lib/antiCheat'
import type { AppBindings } from '../lib/types'

export const runs = new Hono<AppBindings>()

const OUTCOMES = ['extracted', 'died', 'aborted'] as const

/**
 * Submit a finished run.
 *
 * Idempotent by design: the client mints the run id before it sends, so a
 * retry after a dropped connection lands on the same primary key and is
 * answered with the stored result instead of scoring twice.
 */
runs.post('/', async (c) => {
  const body = (await c.req.json().catch(() => {
    throw new ApiError(400, 'invalid_json', 'Body must be JSON')
  })) as Record<string, unknown>

  const clientVersion = str(body.clientVersion, 'clientVersion', { max: 32 })
  if (compareVersions(clientVersion, c.env.MIN_CLIENT_VERSION) < 0) {
    throw new ApiError(409, 'client_outdated', `Update to ${c.env.MIN_CLIENT_VERSION} or newer`)
  }

  const run = {
    id: uuid(body.id, 'id'),
    playerId: uuid(body.playerId, 'playerId'),
    missionId: str(body.missionId, 'missionId', { max: 64 }),
    score: int(body.score, 'score', { max: 10_000_000 }),
    durationMs: int(body.durationMs, 'durationMs', { max: 24 * 60 * 60 * 1000 }),
    kills: int(body.kills, 'kills', { max: 100_000 }),
    shotsFired: int(body.shotsFired, 'shotsFired', { max: 1_000_000 }),
    shotsHit: int(body.shotsHit, 'shotsHit', { max: 1_000_000 }),
    damageTaken: int(body.damageTaken, 'damageTaken', { max: 1_000_000 }),
    peakAlert: int(body.peakAlert, 'peakAlert', { max: 10 }),
    outcome: oneOf(body.outcome, 'outcome', OUTCOMES),
    platform: str(body.platform, 'platform', { max: 32 }),
  }

  const signature = str(body.signature, 'signature', { max: 128 })
  const signedOk = await verify(c.env.RUN_HMAC_SECRET, runSignaturePayload(run), signature)
  if (!signedOk) throw new ApiError(401, 'bad_signature', 'Signature does not match payload')

  const player = await c.env.DB.prepare('SELECT banned FROM players WHERE id = ?1')
    .bind(run.playerId)
    .first<{ banned: number }>()
  if (!player) throw new ApiError(404, 'unknown_player', 'Register the player first')
  if (player.banned) throw new ApiError(403, 'banned', 'This player is banned')

  const existing = await c.env.DB.prepare('SELECT score, flags FROM runs WHERE id = ?1')
    .bind(run.id)
    .first<{ score: number; flags: string }>()
  if (existing) {
    return c.json({ id: run.id, duplicate: true, accepted: existing.flags === '' })
  }

  const flags = flagRun(run)
  const ts = now()

  // One batch so a crash between the insert and the profile update cannot
  // leave best_score disagreeing with the runs table.
  const statements = [
    c.env.DB.prepare(
      `INSERT INTO runs (id, player_id, mission_id, score, duration_ms, kills,
                         shots_fired, shots_hit, damage_taken, peak_alert, outcome,
                         client_version, platform, submitted_at, flags)
       VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11,?12,?13,?14,?15)`,
    ).bind(
      run.id, run.playerId, run.missionId, run.score, run.durationMs, run.kills,
      run.shotsFired, run.shotsHit, run.damageTaken, run.peakAlert, run.outcome,
      clientVersion, run.platform, ts, flags.join(','),
    ),
    c.env.DB.prepare(
      `UPDATE players
       SET total_runs = total_runs + 1,
           last_seen_at = ?2,
           best_score = CASE WHEN ?3 > best_score THEN ?3 ELSE best_score END
       WHERE id = ?1`,
    ).bind(run.playerId, ts, flags.length === 0 ? run.score : 0),
  ]

  await c.env.DB.batch(statements)

  // Flags are returned so the client can log them; the player is not told,
  // because telling a cheat exactly which rule caught them is free QA for them.
  return c.json({ id: run.id, accepted: flags.length === 0, flags }, 201)
})

runs.get('/:id', async (c) => {
  const id = uuid(c.req.param('id'), 'id')
  const row = await c.env.DB.prepare(
    `SELECT r.id, r.mission_id AS missionId, r.score, r.duration_ms AS durationMs,
            r.kills, r.outcome, r.submitted_at AS submittedAt, p.display_name AS playerName
     FROM runs r JOIN players p ON p.id = r.player_id
     WHERE r.id = ?1`,
  )
    .bind(id)
    .first()
  if (!row) throw new ApiError(404, 'not_found', 'No such run')
  return c.json(row)
})

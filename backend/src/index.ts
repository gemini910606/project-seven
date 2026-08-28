import { Hono } from 'hono'
import { cors } from 'hono/cors'
import { health } from './routes/health'
import { players } from './routes/players'
import { runs } from './routes/runs'
import { leaderboard } from './routes/leaderboard'
import { config } from './routes/config'
import { ApiError, allowedOrigins, now } from './lib/http'
import { str } from './lib/validate'
import type { AppBindings } from './lib/types'

export { LobbyRoom } from './do/LobbyRoom'

const app = new Hono<AppBindings>()

app.use('*', async (c, next) => {
  const handler = cors({
    origin: (origin) => (allowedOrigins(c.env).includes(origin) ? origin : null),
    allowMethods: ['GET', 'POST', 'OPTIONS'],
    allowHeaders: ['Content-Type'],
    maxAge: 86400,
  })
  return handler(c, next)
})

app.route('/v1/health', health)
app.route('/v1/players', players)
app.route('/v1/runs', runs)
app.route('/v1/leaderboard', leaderboard)
app.route('/v1/config', config)

/**
 * Fire-and-forget telemetry. Deliberately unauthenticated and deliberately
 * lossy: it is better to drop an event than to make the game wait on it, and
 * an attacker gains nothing from writing junk into a table you only read as
 * aggregates. Keep it free of anything that identifies a person.
 */
app.post('/v1/telemetry', async (c) => {
  const body = (await c.req.json().catch(() => ({}))) as Record<string, unknown>
  const kind = str(body.kind ?? 'unknown', 'kind', { max: 48 })
  const payload = JSON.stringify(body.payload ?? {}).slice(0, 4096)
  const playerId = typeof body.playerId === 'string' ? body.playerId.slice(0, 64) : null

  await c.env.DB.prepare(
    'INSERT INTO telemetry_events (player_id, kind, payload, created_at) VALUES (?1,?2,?3,?4)',
  )
    .bind(playerId, kind, payload, now())
    .run()

  return c.body(null, 204)
})

/** Opens a lobby socket. `name` addresses the room; anything can be a room name. */
app.get('/v1/lobby/:name', async (c) => {
  const name = str(c.req.param('name'), 'name', { max: 64 })
  const id = c.env.LOBBY.idFromName(name)
  return c.env.LOBBY.get(id).fetch(c.req.raw)
})

app.notFound((c) => c.json({ error: { code: 'not_found', message: 'No such route' } }, 404))

app.onError((err, c) => {
  if (err instanceof ApiError) {
    return c.json({ error: { code: err.code, message: err.message } }, err.status)
  }
  // Log the real error for `wrangler tail`, return a generic one to the client:
  // stack traces are a gift to anyone probing the API.
  console.error('unhandled', err)
  return c.json({ error: { code: 'internal', message: 'Internal error' } }, 500)
})

export default app

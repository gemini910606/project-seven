import { Hono } from 'hono'
import type { AppBindings } from '../lib/types'

export const config = new Hono<AppBindings>()

/**
 * Remote config, read by the game at boot. Lets you retune balance, flip a
 * feature flag or post a "servers down" notice without shipping a patch -
 * which is the single highest-leverage thing a tiny backend can do for a game.
 *
 * Edit with:  npx wrangler kv key put --binding=CONFIG live '{"...":...}'
 */
config.get('/', async (c) => {
  const raw = await c.env.CONFIG.get('live')
  const defaults = {
    motd: '',
    leaderboardEnabled: true,
    telemetryEnabled: true,
    minClientVersion: c.env.MIN_CLIENT_VERSION,
  }

  if (!raw) return c.json(defaults)

  try {
    return c.json({ ...defaults, ...(JSON.parse(raw) as Record<string, unknown>) })
  } catch {
    // A malformed config must never brick the game's boot path.
    return c.json(defaults)
  }
})

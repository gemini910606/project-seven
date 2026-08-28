export interface Env {
  DB: D1Database
  CONFIG: KVNamespace
  BUILDS: R2Bucket
  LOBBY: DurableObjectNamespace

  ENVIRONMENT: string
  ALLOWED_ORIGINS: string
  MIN_CLIENT_VERSION: string

  /** Shared secret the game client signs run submissions with. Set via `wrangler secret put`. */
  RUN_HMAC_SECRET: string
  /** Cloudflare Turnstile secret. Empty string disables the check (local dev). */
  TURNSTILE_SECRET: string
}

export type AppBindings = { Bindings: Env }

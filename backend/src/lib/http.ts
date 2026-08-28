/** A typed error that route handlers throw and the top-level handler renders. */
export class ApiError extends Error {
  constructor(
    readonly status: 400 | 401 | 403 | 404 | 409 | 422 | 429 | 500,
    readonly code: string,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

export function badRequest(code: string, message: string): ApiError {
  return new ApiError(400, code, message)
}

/**
 * Reads the allow-list from the environment. The native game client sends no
 * Origin header at all, so CORS only ever gates the browser (WebGL) build.
 */
export function allowedOrigins(env: { ALLOWED_ORIGINS: string }): string[] {
  return env.ALLOWED_ORIGINS.split(',')
    .map((o) => o.trim())
    .filter(Boolean)
}

/** Milliseconds since epoch, as an integer, for D1 storage. */
export function now(): number {
  return Date.now()
}

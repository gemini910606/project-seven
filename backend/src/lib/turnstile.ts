/**
 * Cloudflare Turnstile verification, used only on the web leaderboard form.
 * The native game client does not solve challenges, so it authenticates with
 * its HMAC signature instead. An empty secret disables the check for local dev.
 */
export async function verifyTurnstile(
  secret: string,
  token: string | undefined,
  remoteIp: string | undefined,
): Promise<boolean> {
  if (!secret) return true
  if (!token) return false

  const form = new FormData()
  form.append('secret', secret)
  form.append('response', token)
  if (remoteIp) form.append('remoteip', remoteIp)

  const res = await fetch('https://challenges.cloudflare.com/turnstile/v0/siteverify', {
    method: 'POST',
    body: form,
  })
  if (!res.ok) return false
  const data = (await res.json()) as { success?: boolean }
  return data.success === true
}

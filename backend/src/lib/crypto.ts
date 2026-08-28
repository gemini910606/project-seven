/**
 * HMAC signing for run submissions.
 *
 * IMPORTANT AND NOT OPTIONAL TO UNDERSTAND: this secret ships inside the game
 * client, so a determined attacker WILL extract it and forge valid signatures.
 * That is fine and expected. The signature raises the cost of casual cheating
 * (curl-ing the endpoint, replaying a captured request with a bigger number)
 * from "trivial" to "you have to reverse the binary". Real protection comes
 * from the plausibility rules in antiCheat.ts, which do not trust the client
 * at all. Never treat a valid signature as proof the numbers are honest.
 */

const encoder = new TextEncoder()

async function keyFor(secret: string): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    'raw',
    encoder.encode(secret),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign', 'verify'],
  )
}

function toHex(buffer: ArrayBuffer): string {
  return [...new Uint8Array(buffer)].map((b) => b.toString(16).padStart(2, '0')).join('')
}

export async function sign(secret: string, message: string): Promise<string> {
  const sig = await crypto.subtle.sign('HMAC', await keyFor(secret), encoder.encode(message))
  return toHex(sig)
}

/**
 * Constant-time-ish verification. crypto.subtle.verify does the comparison
 * inside the runtime, which avoids the early-exit leak of a plain === on hex.
 */
export async function verify(secret: string, message: string, hexSignature: string): Promise<boolean> {
  if (!/^[0-9a-f]+$/i.test(hexSignature) || hexSignature.length % 2 !== 0) return false
  const bytes = new Uint8Array(hexSignature.length / 2)
  for (let i = 0; i < bytes.length; i++) {
    bytes[i] = Number.parseInt(hexSignature.slice(i * 2, i * 2 + 2), 16)
  }
  return crypto.subtle.verify('HMAC', await keyFor(secret), bytes, encoder.encode(message))
}

/**
 * The exact string the client must sign. Field order is part of the contract -
 * BackendClient.cs in the Unity project builds the same string. Change one side
 * and every submission starts failing, so change both together.
 */
export function runSignaturePayload(run: {
  id: string
  playerId: string
  missionId: string
  score: number
  durationMs: number
  kills: number
}): string {
  return [run.id, run.playerId, run.missionId, run.score, run.durationMs, run.kills].join('|')
}

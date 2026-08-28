import { describe, expect, it } from 'vitest'
import { runSignaturePayload, sign, verify } from '../src/lib/crypto'

const SECRET = 'test-secret'

const run = {
  id: 'a7e70f32-2091-416c-8da9-1546b4dff1bb',
  playerId: '7b1fe6a8-89ea-491f-b59f-1d2aa48bbe79',
  missionId: 'dockside-raid',
  score: 4200,
  durationMs: 360_000,
  kills: 14,
}

describe('run signatures', () => {
  it('round-trips a signature it produced', async () => {
    const payload = runSignaturePayload(run)
    expect(await verify(SECRET, payload, await sign(SECRET, payload))).toBe(true)
  })

  it('rejects a signature made with a different secret', async () => {
    const payload = runSignaturePayload(run)
    expect(await verify(SECRET, payload, await sign('other-secret', payload))).toBe(false)
  })

  it('rejects a payload whose score was edited after signing', async () => {
    const signature = await sign(SECRET, runSignaturePayload(run))
    const tampered = runSignaturePayload({ ...run, score: 999_999 })
    expect(await verify(SECRET, tampered, signature)).toBe(false)
  })

  it('rejects malformed signature strings without throwing', async () => {
    const payload = runSignaturePayload(run)
    expect(await verify(SECRET, payload, 'not-hex')).toBe(false)
    expect(await verify(SECRET, payload, 'abc')).toBe(false)
    expect(await verify(SECRET, payload, '')).toBe(false)
  })

  it('pins the payload format the Unity client must reproduce', () => {
    expect(runSignaturePayload(run)).toBe(
      'a7e70f32-2091-416c-8da9-1546b4dff1bb|7b1fe6a8-89ea-491f-b59f-1d2aa48bbe79|dockside-raid|4200|360000|14',
    )
  })
})

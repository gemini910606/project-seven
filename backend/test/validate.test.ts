import { describe, expect, it } from 'vitest'
import { compareVersions, displayName, int, oneOf, str, uuid } from '../src/lib/validate'
import { ApiError } from '../src/lib/http'

describe('str', () => {
  it('accepts a string inside the bounds', () => {
    expect(str('hello', 'field')).toBe('hello')
  })
  it('rejects a non-string', () => {
    expect(() => str(42, 'field')).toThrow(ApiError)
  })
  it('rejects an over-long string', () => {
    expect(() => str('abcdef', 'field', { max: 3 })).toThrow(/3 characters/)
  })
})

describe('int', () => {
  it('rejects a float', () => {
    expect(() => int(1.5, 'score')).toThrow(/must be an integer/)
  })
  it('rejects a value past the ceiling', () => {
    expect(() => int(11, 'score', { max: 10 })).toThrow(/between 0 and 10/)
  })
  it('accepts the boundary', () => {
    expect(int(10, 'score', { max: 10 })).toBe(10)
  })
})

describe('oneOf', () => {
  it('accepts a member', () => {
    expect(oneOf('died', 'outcome', ['extracted', 'died'] as const)).toBe('died')
  })
  it('names the allowed values when it rejects', () => {
    expect(() => oneOf('exploded', 'outcome', ['extracted', 'died'] as const)).toThrow(
      /extracted, died/,
    )
  })
})

describe('uuid', () => {
  it('normalises case', () => {
    expect(uuid('A7E70F32-2091-416C-8DA9-1546B4DFF1BB', 'id')).toBe(
      'a7e70f32-2091-416c-8da9-1546b4dff1bb',
    )
  })
  it('rejects a non-uuid of the right length', () => {
    expect(() => uuid('x'.repeat(36), 'id')).toThrow(/must be a UUID/)
  })
})

describe('displayName', () => {
  it('collapses whitespace and trims', () => {
    expect(displayName('  Big   Smoke  ')).toBe('Big Smoke')
  })

  it('strips zero-width padding used to impersonate another player', () => {
    const zwsp = String.fromCharCode(0x200b)
    expect(displayName(`Sweet${zwsp}${zwsp}`)).toBe('Sweet')
  })

  it('strips bidi overrides', () => {
    const rlo = String.fromCharCode(0x202e)
    expect(displayName(`${rlo}Ryder`)).toBe('Ryder')
  })

  it('rejects a name that is only invisible characters', () => {
    const zwsp = String.fromCharCode(0x200b)
    expect(() => displayName(zwsp.repeat(10))).toThrow(/too short after cleaning/)
  })
})

describe('compareVersions', () => {
  it('orders by numeric component, not lexically', () => {
    expect(compareVersions('0.10.0', '0.9.0')).toBeGreaterThan(0)
  })
  it('treats missing components as zero', () => {
    expect(compareVersions('1.2', '1.2.0')).toBe(0)
  })
  it('detects an older client', () => {
    expect(compareVersions('0.0.9', '0.1.0')).toBeLessThan(0)
  })
})

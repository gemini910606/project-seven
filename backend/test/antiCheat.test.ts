import { describe, expect, it } from 'vitest'
import { flagRun, LIMITS, type RunFacts } from '../src/lib/antiCheat'

/** A run that a competent human could actually produce. */
function honestRun(overrides: Partial<RunFacts> = {}): RunFacts {
  return {
    score: 4200,
    durationMs: 6 * 60 * 1000,
    kills: 14,
    shotsFired: 210,
    shotsHit: 96,
    damageTaken: 130,
    peakAlert: 3,
    outcome: 'extracted',
    ...overrides,
  }
}

describe('flagRun', () => {
  it('passes a plausible run', () => {
    expect(flagRun(honestRun())).toEqual([])
  })

  it('rejects a run shorter than the extraction walk', () => {
    expect(flagRun(honestRun({ durationMs: LIMITS.minDurationMs - 1 }))).toContain(
      'duration_too_short',
    )
  })

  it('rejects a score above what the kills could possibly be worth', () => {
    const kills = 2
    const impossible = kills * LIMITS.maxScorePerKill + LIMITS.maxObjectiveScore + 1
    expect(flagRun(honestRun({ kills, score: impossible, shotsHit: 2, shotsFired: 4 }))).toContain(
      'score_exceeds_ceiling',
    )
  })

  it('allows a zero-kill stealth run to score up to the objective ceiling', () => {
    const stealth = honestRun({
      kills: 0,
      shotsFired: 0,
      shotsHit: 0,
      score: LIMITS.maxObjectiveScore,
    })
    expect(flagRun(stealth)).toEqual([])
  })

  it('catches more hits than shots', () => {
    expect(flagRun(honestRun({ shotsFired: 10, shotsHit: 11, kills: 5 }))).toContain(
      'hits_exceed_shots',
    )
  })

  it('catches more kills than hits', () => {
    expect(flagRun(honestRun({ shotsFired: 20, shotsHit: 5, kills: 6 }))).toContain(
      'kills_exceed_hits',
    )
  })

  it('catches an impossible sustained fire rate', () => {
    const seconds = 60
    const run = honestRun({
      durationMs: seconds * 1000,
      shotsFired: seconds * (LIMITS.maxShotsPerSecond + 5),
      shotsHit: 10,
      kills: 5,
      score: 1000,
    })
    expect(flagRun(run)).toContain('fire_rate_impossible')
  })

  it('catches an impossible kill rate', () => {
    const minutes = 1
    const kills = LIMITS.maxKillsPerMinute + 10
    const run = honestRun({
      durationMs: minutes * 60_000,
      kills,
      shotsHit: kills,
      shotsFired: kills * 2,
      score: kills * 100,
    })
    expect(flagRun(run)).toContain('kill_rate_impossible')
  })

  it('refuses to let an aborted run carry points', () => {
    expect(flagRun(honestRun({ outcome: 'aborted', score: 1 }))).toContain('aborted_with_score')
  })

  it('accepts an aborted run worth nothing', () => {
    const run = honestRun({ outcome: 'aborted', score: 0, kills: 0, shotsFired: 0, shotsHit: 0 })
    expect(flagRun(run)).toEqual([])
  })

  it('rejects an alert level outside the 0-5 scale the client owns', () => {
    expect(flagRun(honestRun({ peakAlert: 9 }))).toContain('alert_out_of_range')
  })
})

/**
 * These pin the contract with ScoreCalculator.cs in the Unity client. The two
 * sides compute scores independently, so nothing but a test stops them drifting
 * apart until a player's perfect run silently fails to rank.
 *
 * Client constants, mirrored:
 *   PointsPerKill 100, max AlertMultiplier 2.0  -> 200 per kill
 *   PointsPerObjective 750
 *   ExtractionBonus 1000 + MaxSpeedBonus 1500 + MaxMarksmanBonus 750 = 3250 flat
 */
describe('score ceiling agrees with the client ScoreCalculator', () => {
  const CLIENT = {
    maxPerKill: 100 * 2.0,
    perObjective: 750,
    flatBonuses: 1000 + 1500 + 750,
    maxObjectivesPerMission: 4,
  }

  it('leaves headroom over the client per-kill payout', () => {
    expect(CLIENT.maxPerKill).toBeLessThanOrEqual(LIMITS.maxScorePerKill)
  })

  it('covers a perfect run on the largest mission without flagging it', () => {
    const clientMax =
      CLIENT.maxObjectivesPerMission * CLIENT.perObjective + CLIENT.flatBonuses
    expect(clientMax).toBeLessThanOrEqual(LIMITS.maxObjectiveScore)
  })

  it('does not flag a flawless 4-objective extraction', () => {
    const kills = 20
    const perfect: RunFacts = {
      score: kills * CLIENT.maxPerKill + CLIENT.maxObjectivesPerMission * CLIENT.perObjective + CLIENT.flatBonuses,
      durationMs: 3 * 60 * 1000,
      kills,
      shotsFired: 120,
      shotsHit: 80,
      damageTaken: 0,
      peakAlert: 5,
      outcome: 'extracted',
    }
    expect(flagRun(perfect)).toEqual([])
  })
})

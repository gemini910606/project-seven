/**
 * Plausibility rules for a submitted run.
 *
 * The client is fully untrusted: it reports its own score, kills and duration.
 * We cannot make those true, but we can make an implausible run cheap to spot
 * and keep it off the leaderboard. Each rule returns a short machine-readable
 * name; a run with any flag is stored (so it can be audited) but excluded from
 * ranked queries.
 *
 * Tune these against real telemetry once you have some. Rules that are too
 * tight will flag your best legitimate players, which is worse than letting a
 * few cheats through.
 */

export interface RunFacts {
  score: number
  durationMs: number
  kills: number
  shotsFired: number
  shotsHit: number
  damageTaken: number
  peakAlert: number
  outcome: 'extracted' | 'died' | 'aborted'
}

/** Design constants that mirror the game's own tuning. Keep in sync with the mission SO. */
export const LIMITS = {
  /** Shortest a run can physically be: the extraction walk alone takes this long. */
  minDurationMs: 20_000,
  /** Anything past this is an idle session, not a run. */
  maxDurationMs: 2 * 60 * 60 * 1000,
  /** Highest points a single kill can be worth, including every multiplier. */
  maxScorePerKill: 500,
  /**
   * Flat ceiling for everything that is not a per-kill payout: objectives, the
   * extraction bonus, the speed bonus and the marksman bonus combined.
   *
   * This MUST stay at or above ScoreCalculator.TheoreticalMax(0, n) in the Unity
   * client for the largest objective count any mission ships with. The vertical
   * slice tops out at 4 objectives = 4*750 + 1000 + 1500 + 750 = 6250, so 8000
   * leaves room for one more objective before this has to move. Set it too low
   * and perfect legitimate runs get flagged and silently vanish from the board,
   * which is indistinguishable from the backend being broken.
   */
  maxObjectiveScore: 8_000,
  /** No weapon in the game can fire faster than this sustained. */
  maxShotsPerSecond: 20,
  /** More kills than this per minute is not reachable with the current spawner. */
  maxKillsPerMinute: 60,
} as const

export function flagRun(run: RunFacts): string[] {
  const flags: string[] = []
  const seconds = run.durationMs / 1000
  const minutes = seconds / 60

  if (run.durationMs < LIMITS.minDurationMs) flags.push('duration_too_short')
  if (run.durationMs > LIMITS.maxDurationMs) flags.push('duration_too_long')

  const scoreCeiling = run.kills * LIMITS.maxScorePerKill + LIMITS.maxObjectiveScore
  if (run.score > scoreCeiling) flags.push('score_exceeds_ceiling')

  if (run.shotsHit > run.shotsFired) flags.push('hits_exceed_shots')

  // A kill needs at least one hit. Melee is not in the vertical slice; add an
  // exemption here rather than loosening the rule when it ships.
  if (run.kills > run.shotsHit) flags.push('kills_exceed_hits')

  if (seconds > 0 && run.shotsFired / seconds > LIMITS.maxShotsPerSecond) {
    flags.push('fire_rate_impossible')
  }

  if (minutes > 0 && run.kills / minutes > LIMITS.maxKillsPerMinute) {
    flags.push('kill_rate_impossible')
  }

  // Alert level is a 0-5 scale owned by the client's AlertSystem.
  if (run.peakAlert < 0 || run.peakAlert > 5) flags.push('alert_out_of_range')

  // An aborted run should never be worth points; scoring one is a client bug
  // or a forged payload, and either way it must not rank.
  if (run.outcome === 'aborted' && run.score > 0) flags.push('aborted_with_score')

  return flags
}

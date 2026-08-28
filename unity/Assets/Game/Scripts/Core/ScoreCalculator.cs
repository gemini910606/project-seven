using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Turns a finished run into a single number.
    ///
    /// The constants here are mirrored by LIMITS in backend/src/lib/antiCheat.ts.
    /// If you raise a payout past the server's ceiling, every legitimate run
    /// starts getting flagged and silently dropped from the leaderboard - which
    /// looks exactly like the backend being broken. Change both together, and
    /// keep ScoreCalculatorTests green; it asserts the ceiling relationship.
    /// </summary>
    public static class ScoreCalculator
    {
        /// <summary>Base points for a kill, before the alert-level multiplier.</summary>
        public const int PointsPerKill = 100;

        /// <summary>Points for finishing one objective.</summary>
        public const int PointsPerObjective = 750;

        /// <summary>Paid once, for getting out alive.</summary>
        public const int ExtractionBonus = 1000;

        /// <summary>Par time in seconds. Finishing faster pays; slower does not penalise.</summary>
        public const float ParSeconds = 480f;

        /// <summary>Maximum the speed bonus can pay.</summary>
        public const int MaxSpeedBonus = 1500;

        /// <summary>Accuracy at or above this pays the full marksman bonus.</summary>
        public const float MarksmanAccuracy = 0.5f;

        public const int MaxMarksmanBonus = 750;

        /// <summary>
        /// Killing while the city is hot is worth more. Capped at 2.0 so the
        /// per-kill payout can never exceed the server's maxScorePerKill of 500.
        /// </summary>
        public static float AlertMultiplier(int alertLevel) =>
            1f + Mathf.Clamp(alertLevel, 0, 5) * 0.2f;

        public static int Compute(RunStats stats)
        {
            if (stats == null) return 0;

            // An abandoned run is worth nothing. The server enforces this too.
            if (stats.Outcome == RunOutcome.Aborted) return 0;

            float killScore = stats.Kills * PointsPerKill * AlertMultiplier(stats.PeakAlert);
            int objectiveScore = stats.ObjectivesCompleted * PointsPerObjective;

            int total = Mathf.RoundToInt(killScore) + objectiveScore;

            // Everything below is only paid for a successful extraction, so dying
            // on the way out costs the whole completion package rather than a
            // token amount. That is what makes the last thirty seconds tense.
            if (stats.Outcome == RunOutcome.Extracted)
            {
                total += ExtractionBonus;

                float overPar = Mathf.Max(0f, ParSeconds - stats.DurationSeconds);
                total += Mathf.RoundToInt(MaxSpeedBonus * Mathf.Clamp01(overPar / ParSeconds));

                if (stats.ShotsFired > 0)
                {
                    float t = Mathf.Clamp01(stats.Accuracy / MarksmanAccuracy);
                    total += Mathf.RoundToInt(MaxMarksmanBonus * t);
                }
            }

            return Mathf.Max(0, total);
        }

        /// <summary>
        /// The largest score the rules above can produce for a given kill count.
        /// The server computes the same ceiling; this exists so a test can prove
        /// the two agree instead of finding out in production.
        /// </summary>
        public static int TheoreticalMax(int kills, int objectives) =>
            Mathf.RoundToInt(kills * PointsPerKill * AlertMultiplier(5))
            + objectives * PointsPerObjective
            + ExtractionBonus
            + MaxSpeedBonus
            + MaxMarksmanBonus;
    }
}

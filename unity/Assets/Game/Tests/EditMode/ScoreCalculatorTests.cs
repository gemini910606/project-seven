using NUnit.Framework;
using Game.Core;

namespace Game.Tests
{
    /// <summary>
    /// These are the client half of a contract with the server. The matching
    /// assertions live in backend/test/antiCheat.test.ts. If you change a payout
    /// here, that file has to change too or perfectly legitimate runs start
    /// getting flagged and vanishing from the leaderboard.
    /// </summary>
    public sealed class ScoreCalculatorTests
    {
        /// <summary>Mirrors LIMITS in backend/src/lib/antiCheat.ts.</summary>
        private const int ServerMaxScorePerKill = 500;
        private const int ServerMaxObjectiveScore = 8000;

        /// <summary>The largest objective count any shipped mission has.</summary>
        private const int MaxObjectivesPerMission = 4;

        private static RunStats Run(
            int kills = 0, int objectives = 0, int peakAlert = 0,
            float duration = 300f, int shotsFired = 0, int shotsHit = 0,
            RunOutcome outcome = RunOutcome.Extracted) => new()
        {
            MissionId = "dockside-raid",
            Kills = kills,
            ObjectivesCompleted = objectives,
            PeakAlert = peakAlert,
            DurationSeconds = duration,
            ShotsFired = shotsFired,
            ShotsHit = shotsHit,
            Outcome = outcome,
        };

        [Test]
        public void AbortedRunScoresNothing()
        {
            RunStats stats = Run(kills: 50, objectives: 4, outcome: RunOutcome.Aborted);
            Assert.AreEqual(0, ScoreCalculator.Compute(stats));
        }

        [Test]
        public void DyingForfeitsTheCompletionBonuses()
        {
            RunStats died = Run(kills: 10, objectives: 2, outcome: RunOutcome.Died);
            RunStats extracted = Run(kills: 10, objectives: 2, outcome: RunOutcome.Extracted);

            int gap = ScoreCalculator.Compute(extracted) - ScoreCalculator.Compute(died);

            // Extraction, speed and marksman bonuses are all withheld on death.
            // That gap is what makes the walk to the exit tense.
            Assert.Greater(gap, ScoreCalculator.ExtractionBonus);
        }

        [Test]
        public void HigherHeatPaysMorePerKill()
        {
            int cold = ScoreCalculator.Compute(Run(kills: 10, peakAlert: 0));
            int hot = ScoreCalculator.Compute(Run(kills: 10, peakAlert: 5));

            Assert.Greater(hot, cold);
        }

        [Test]
        public void AlertMultiplierNeverExceedsTwo()
        {
            // The server's per-kill ceiling assumes this. Raising it silently
            // breaks submissions rather than failing loudly.
            Assert.AreEqual(2f, ScoreCalculator.AlertMultiplier(5), 0.0001f);
            Assert.AreEqual(2f, ScoreCalculator.AlertMultiplier(99), 0.0001f);
        }

        [Test]
        public void SpeedBonusIsZeroAtOrPastPar()
        {
            int atPar = ScoreCalculator.Compute(Run(duration: ScoreCalculator.ParSeconds));
            int slow = ScoreCalculator.Compute(Run(duration: ScoreCalculator.ParSeconds * 3f));

            Assert.AreEqual(atPar, slow, "Running over par must not keep subtracting points.");
        }

        [Test]
        public void FasterRunsScoreHigher()
        {
            int quick = ScoreCalculator.Compute(Run(duration: 60f));
            int slow = ScoreCalculator.Compute(Run(duration: 400f));

            Assert.Greater(quick, slow);
        }

        [Test]
        public void ScoreIsNeverNegative()
        {
            Assert.GreaterOrEqual(ScoreCalculator.Compute(Run(duration: 100000f)), 0);
        }

        [Test]
        public void PerKillPayoutStaysUnderTheServerCeiling()
        {
            const int kills = 100;
            int killOnly = ScoreCalculator.Compute(
                Run(kills: kills, peakAlert: 5, duration: ScoreCalculator.ParSeconds, outcome: RunOutcome.Died));

            Assert.LessOrEqual(
                killOnly / (float)kills, ServerMaxScorePerKill,
                "Per-kill payout exceeds the server's maxScorePerKill; every high-kill run would be flagged.");
        }

        [Test]
        public void PerfectRunStaysUnderTheServerObjectiveCeiling()
        {
            int nonKillPortion = ScoreCalculator.TheoreticalMax(0, MaxObjectivesPerMission);

            Assert.LessOrEqual(
                nonKillPortion, ServerMaxObjectiveScore,
                "A flawless run scores above the server's maxObjectiveScore and would be flagged as a cheat.");
        }

        [Test]
        public void TheoreticalMaxBoundsRealScores()
        {
            RunStats best = Run(
                kills: 25, objectives: MaxObjectivesPerMission, peakAlert: 5,
                duration: 1f, shotsFired: 100, shotsHit: 100);

            Assert.LessOrEqual(
                ScoreCalculator.Compute(best),
                ScoreCalculator.TheoreticalMax(25, MaxObjectivesPerMission));
        }
    }
}

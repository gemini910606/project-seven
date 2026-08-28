using System.Collections.Generic;
using NUnit.Framework;
using Game.Round.Rules;

namespace Game.Tests
{
    /// <summary>
    /// Round rules, which is where a shooter's most embarrassing bugs live.
    ///
    /// This file compiles twice: in Unity's Test Runner, and in tools/RulesTests
    /// against the same source with plain dotnet. The second one is why these
    /// assertions are known to pass rather than merely believed to.
    /// </summary>
    public sealed class MatchCoreTests
    {
        private MatchRules _rules;
        private MatchCore _match;

        [SetUp]
        public void SetUp()
        {
            _rules = new MatchRules
            {
                RoundsToWin = 4,
                TeamSize = 5,
                PrepSeconds = 5f,
                RoundSeconds = 60f,
                SpikeSeconds = 30f,
                RoundOverSeconds = 3f,
            };
            _match = new MatchCore(_rules);
            _match.StartMatch();
        }

        /// <summary>Advances in small steps so a phase change mid-interval is not skipped.</summary>
        private void Advance(float seconds)
        {
            const float step = 0.25f;
            for (float t = 0f; t < seconds; t += step) _match.Tick(step);
        }

        private void GoLive()
        {
            Advance(_rules.PrepSeconds + 0.5f);
            Assert.AreEqual(RoundPhase.Live, _match.Phase, "Expected the round to be live by now.");
        }

        private void StartNextRound()
        {
            Advance(_rules.RoundOverSeconds + 0.5f);
            Assert.AreEqual(RoundPhase.Prep, _match.Phase);
        }

        // ------------------------------------------------------------------
        // Phases
        // ------------------------------------------------------------------

        [Test]
        public void MatchStartsInPrepOnRoundOne()
        {
            Assert.AreEqual(RoundPhase.Prep, _match.Phase);
            Assert.AreEqual(1, _match.RoundNumber);
            Assert.AreEqual(0, _match.TeamAScore);
            Assert.AreEqual(0, _match.TeamBScore);
        }

        [Test]
        public void PrepBecomesLiveAndStartsTheRoundClock()
        {
            GoLive();
            Assert.AreEqual(_rules.RoundSeconds, _match.PhaseSecondsRemaining, 1f);
        }

        [Test]
        public void StartMatchIsIdempotent()
        {
            _match.StartMatch();
            Assert.AreEqual(1, _match.RoundNumber, "A second StartMatch must not restart the round.");
        }

        // ------------------------------------------------------------------
        // Winning a live round
        // ------------------------------------------------------------------

        [Test]
        public void WipingTheAttackersBeforeAPlantGivesDefendersTheRound()
        {
            GoLive();
            _match.ReportAlive(Side.Attackers, 0);

            Assert.AreEqual(RoundPhase.Over, _match.Phase);
            Assert.AreEqual(1, _match.ScoreOf(_match.TeamOn(Side.Defenders)));
        }

        [Test]
        public void WipingTheDefendersGivesAttackersTheRound()
        {
            GoLive();
            _match.ReportAlive(Side.Defenders, 0);

            Assert.AreEqual(RoundPhase.Over, _match.Phase);
            Assert.AreEqual(1, _match.ScoreOf(_match.TeamOn(Side.Attackers)));
        }

        [Test]
        public void RunningOutTheClockWithNoPlantGivesDefendersTheRound()
        {
            GoLive();

            RoundResult? seen = null;
            _match.RoundEnded += r => seen = r;

            Advance(_rules.RoundSeconds + 1f);

            Assert.IsTrue(seen.HasValue);
            Assert.AreEqual(Side.Defenders, seen.Value.WinningSide);
            Assert.AreEqual(RoundWinReason.TimeExpired, seen.Value.Reason);
        }

        // ------------------------------------------------------------------
        // The spike, and the rule everyone gets wrong
        // ------------------------------------------------------------------

        [Test]
        public void PlantingSwitchesToTheSpikeClock()
        {
            GoLive();
            Advance(20f);
            _match.ReportSpikePlanted();

            Assert.AreEqual(RoundPhase.PostPlant, _match.Phase);
            Assert.IsTrue(_match.SpikePlanted);
            Assert.AreEqual(_rules.SpikeSeconds, _match.PhaseSecondsRemaining, 1f,
                "The spike clock must replace the round clock, not continue it.");
        }

        [Test]
        public void TheRoundClockStopsMatteringOnceTheSpikeIsDown()
        {
            GoLive();
            Advance(_rules.RoundSeconds - 2f);
            _match.ReportSpikePlanted();

            // Well past when the round timer would have expired.
            Advance(5f);

            Assert.AreEqual(RoundPhase.PostPlant, _match.Phase,
                "Defenders must not win on the round clock after a plant.");
        }

        [Test]
        public void WipingTheAttackersAfterAPlantDoesNotEndTheRound()
        {
            GoLive();
            _match.ReportSpikePlanted();
            _match.ReportAlive(Side.Attackers, 0);

            Assert.AreEqual(RoundPhase.PostPlant, _match.Phase,
                "With the spike down the defenders still have to defuse it.");
            Assert.AreEqual(0, _match.TeamAScore);
            Assert.AreEqual(0, _match.TeamBScore);
        }

        [Test]
        public void DeadAttackersStillWinIfTheSpikeDetonates()
        {
            GoLive();
            _match.ReportSpikePlanted();
            _match.ReportAlive(Side.Attackers, 0);

            MatchTeam attackers = _match.TeamOn(Side.Attackers);
            Advance(_rules.SpikeSeconds + 1f);

            Assert.AreEqual(1, _match.ScoreOf(attackers));
        }

        [Test]
        public void WipingTheDefendersAfterAPlantEndsItImmediately()
        {
            GoLive();
            _match.ReportSpikePlanted();

            RoundResult? seen = null;
            _match.RoundEnded += r => seen = r;

            _match.ReportAlive(Side.Defenders, 0);

            Assert.IsTrue(seen.HasValue, "Nobody is left who could defuse.");
            Assert.AreEqual(RoundWinReason.Elimination, seen.Value.Reason);
            Assert.AreEqual(Side.Attackers, seen.Value.WinningSide);
        }

        [Test]
        public void DetonationGivesAttackersTheRound()
        {
            GoLive();
            _match.ReportSpikePlanted();

            RoundResult? seen = null;
            _match.RoundEnded += r => seen = r;

            Advance(_rules.SpikeSeconds + 1f);

            Assert.IsTrue(seen.HasValue);
            Assert.AreEqual(RoundWinReason.SpikeDetonated, seen.Value.Reason);
        }

        [Test]
        public void DefusingGivesDefendersTheRound()
        {
            GoLive();
            _match.ReportSpikePlanted();

            RoundResult? seen = null;
            _match.RoundEnded += r => seen = r;

            _match.ReportSpikeDefused();

            Assert.IsTrue(seen.HasValue);
            Assert.AreEqual(RoundWinReason.SpikeDefused, seen.Value.Reason);
            Assert.AreEqual(Side.Defenders, seen.Value.WinningSide);
        }

        [Test]
        public void PlantingIsIgnoredOutsideALiveRound()
        {
            // Still in Prep.
            _match.ReportSpikePlanted();
            Assert.AreEqual(RoundPhase.Prep, _match.Phase);
            Assert.IsFalse(_match.SpikePlanted);
        }

        [Test]
        public void DefusingIsIgnoredBeforeAPlant()
        {
            GoLive();
            _match.ReportSpikeDefused();

            Assert.AreEqual(RoundPhase.Live, _match.Phase);
            Assert.AreEqual(0, _match.TeamAScore);
            Assert.AreEqual(0, _match.TeamBScore);
        }

        [Test]
        public void TheSpikeIsClearedForTheNextRound()
        {
            GoLive();
            _match.ReportSpikePlanted();
            _match.ReportSpikeDefused();
            StartNextRound();

            Assert.IsFalse(_match.SpikePlanted);
            Assert.AreEqual(2, _match.RoundNumber);
        }

        // ------------------------------------------------------------------
        // Sides, teams and the halftime swap
        // ------------------------------------------------------------------

        [Test]
        public void TeamAStartsOnAttack()
        {
            Assert.AreEqual(Side.Attackers, _match.SideOf(MatchTeam.A));
            Assert.AreEqual(Side.Defenders, _match.SideOf(MatchTeam.B));
        }

        [Test]
        public void SideAndTeamLookupsAreInverses()
        {
            Assert.AreEqual(MatchTeam.A, _match.TeamOn(_match.SideOf(MatchTeam.A)));
            Assert.AreEqual(MatchTeam.B, _match.TeamOn(_match.SideOf(MatchTeam.B)));
        }

        [Test]
        public void SidesSwapAfterTheConfiguredNumberOfRounds()
        {
            for (int i = 0; i < _rules.RoundsBeforeSwap; i++)
            {
                GoLive();
                // Alternate the winner so nobody reaches the match point early.
                _match.ReportAlive(i % 2 == 0 ? Side.Attackers : Side.Defenders, 0);
                StartNextRound();
            }

            Assert.IsTrue(_match.SidesSwapped);
            Assert.AreEqual(Side.Defenders, _match.SideOf(MatchTeam.A));
            Assert.AreEqual(Side.Attackers, _match.SideOf(MatchTeam.B));
        }

        [Test]
        public void HalftimeFiresExactlyOnce()
        {
            int halftimes = 0;
            _match.HalftimeReached += () => halftimes++;

            for (int i = 0; i < _rules.RoundsBeforeSwap + 1; i++)
            {
                GoLive();
                _match.ReportAlive(i % 2 == 0 ? Side.Attackers : Side.Defenders, 0);
                StartNextRound();
            }

            Assert.AreEqual(1, halftimes);
        }

        [Test]
        public void ScoreFollowsTheTeamAcrossTheSwapNotTheSide()
        {
            // Team A wins every round it can, from both sides.
            var winners = new List<MatchTeam>();
            _match.RoundEnded += r => winners.Add(r.WinningTeam);

            for (int i = 0; i < _rules.RoundsBeforeSwap + 1; i++)
            {
                GoLive();
                // Wipe whichever side team A is NOT on.
                Side losingSide = _match.SideOf(MatchTeam.A) == Side.Attackers
                    ? Side.Defenders
                    : Side.Attackers;
                _match.ReportAlive(losingSide, 0);
                if (!_match.IsMatchOver) StartNextRound();
            }

            CollectionAssert.AreEqual(
                new[] { MatchTeam.A, MatchTeam.A, MatchTeam.A, MatchTeam.A },
                winners,
                "Team A won every round, so every round must be credited to team A.");
        }

        // ------------------------------------------------------------------
        // Ending the match
        // ------------------------------------------------------------------

        [Test]
        public void MatchEndsWhenATeamReachesTheRequiredWins()
        {
            MatchTeam ended = MatchTeam.None;
            _match.MatchEnded += t => ended = t;

            for (int i = 0; i < _rules.RoundsToWin; i++)
            {
                GoLive();
                Side losingSide = _match.SideOf(MatchTeam.A) == Side.Attackers
                    ? Side.Defenders
                    : Side.Attackers;
                _match.ReportAlive(losingSide, 0);
                if (!_match.IsMatchOver) StartNextRound();
            }

            Assert.IsTrue(_match.IsMatchOver);
            Assert.AreEqual(MatchTeam.A, _match.Winner);
            Assert.AreEqual(MatchTeam.A, ended);
            Assert.AreEqual(_rules.RoundsToWin, _match.TeamAScore);
        }

        [Test]
        public void NothingHappensAfterTheMatchIsOver()
        {
            for (int i = 0; i < _rules.RoundsToWin; i++)
            {
                GoLive();
                Side losingSide = _match.SideOf(MatchTeam.A) == Side.Attackers
                    ? Side.Defenders
                    : Side.Attackers;
                _match.ReportAlive(losingSide, 0);
                if (!_match.IsMatchOver) StartNextRound();
            }

            int roundsAtEnd = _match.RoundNumber;
            Advance(120f);

            Assert.AreEqual(roundsAtEnd, _match.RoundNumber, "No round may start after the match ends.");
            Assert.AreEqual(_rules.RoundsToWin, _match.TeamAScore);
        }

        [Test]
        public void AMatchCannotExceedItsMaximumRounds()
        {
            // Alternate winners for as long as possible, then let it resolve.
            int guard = 0;
            while (!_match.IsMatchOver && guard++ < 100)
            {
                GoLive();
                _match.ReportAlive(guard % 2 == 0 ? Side.Attackers : Side.Defenders, 0);
                if (!_match.IsMatchOver) StartNextRound();
            }

            Assert.IsTrue(_match.IsMatchOver);
            Assert.LessOrEqual(_match.RoundsPlayed, _rules.MaxRounds);
        }

        [Test]
        public void DefaultRulesMatchTheDocumentedShape()
        {
            var defaults = new MatchRules();
            Assert.AreEqual(6, defaults.RoundsBeforeSwap, "Swap should be one short of the win target.");
            Assert.AreEqual(13, defaults.MaxRounds);
            Assert.AreEqual(5, defaults.TeamSize);
        }
    }
}

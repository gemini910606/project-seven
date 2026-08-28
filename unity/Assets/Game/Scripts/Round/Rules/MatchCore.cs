using System;

namespace Game.Round.Rules
{
    /// <summary>
    /// The whole match, as a tickable state machine with no engine dependency.
    ///
    /// This class owns every rule that decides who wins: phase transitions, the
    /// two independent clocks, side swapping, scoring and match end. The Unity
    /// layer feeds it facts (how many players are alive, the spike was planted)
    /// and reacts to its events. It never asks Unity anything.
    ///
    /// That split exists so these rules can be compiled and tested without the
    /// editor - see tools/RulesTests. Round logic is where a shooter's most
    /// embarrassing bugs live, and they are all reachable by a unit test if the
    /// rules are not tangled up in MonoBehaviours.
    ///
    /// Runs on the host only. Clients are told the outcome; they do not compute it.
    /// </summary>
    public sealed class MatchCore
    {
        private readonly MatchRules _rules;

        private int _attackersAlive;
        private int _defendersAlive;
        private bool _spikePlanted;
        private bool _started;

        public MatchCore(MatchRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Phase = RoundPhase.Prep;
        }

        public MatchRules Rules => _rules;

        public RoundPhase Phase { get; private set; }

        /// <summary>Seconds left on whichever clock currently governs the phase.</summary>
        public float PhaseSecondsRemaining { get; private set; }

        /// <summary>1-based. Zero until the match starts.</summary>
        public int RoundNumber { get; private set; }

        public int RoundsPlayed { get; private set; }

        public int TeamAScore { get; private set; }

        public int TeamBScore { get; private set; }

        /// <summary>True once the halftime swap has happened.</summary>
        public bool SidesSwapped { get; private set; }

        public bool IsMatchOver { get; private set; }

        public MatchTeam Winner { get; private set; } = MatchTeam.None;

        public bool SpikePlanted => _spikePlanted;

        /// <summary>Fired at the start of each round with its 1-based number.</summary>
        public event Action<int> RoundStarted;

        /// <summary>Fired the moment a round is decided.</summary>
        public event Action<RoundResult> RoundEnded;

        /// <summary>Fired once, when a team reaches the required round wins.</summary>
        public event Action<MatchTeam> MatchEnded;

        /// <summary>Fired when sides swap at halftime, so spawns and UI can flip.</summary>
        public event Action HalftimeReached;

        // ------------------------------------------------------------------
        // Side and team mapping
        // ------------------------------------------------------------------

        /// <summary>Team A starts on attack; after the swap it defends.</summary>
        public Side SideOf(MatchTeam team)
        {
            if (team == MatchTeam.None) throw new ArgumentException("No side for MatchTeam.None", nameof(team));

            bool isTeamA = team == MatchTeam.A;
            bool attacking = SidesSwapped ? !isTeamA : isTeamA;
            return attacking ? Side.Attackers : Side.Defenders;
        }

        public MatchTeam TeamOn(Side side)
        {
            bool attackers = side == Side.Attackers;
            bool isTeamA = SidesSwapped ? !attackers : attackers;
            return isTeamA ? MatchTeam.A : MatchTeam.B;
        }

        public int ScoreOf(MatchTeam team) => team == MatchTeam.A ? TeamAScore : TeamBScore;

        // ------------------------------------------------------------------
        // Driving the match
        // ------------------------------------------------------------------

        public void StartMatch()
        {
            if (_started) return;

            _started = true;
            RoundNumber = 0;
            BeginNextRound();
        }

        /// <summary>
        /// Host tells the core how many players on each side are still alive.
        /// Called whenever someone dies, not every frame.
        /// </summary>
        public void ReportAlive(Side side, int aliveCount)
        {
            if (aliveCount < 0) throw new ArgumentOutOfRangeException(nameof(aliveCount));

            if (side == Side.Attackers) _attackersAlive = aliveCount;
            else _defendersAlive = aliveCount;

            if (Phase == RoundPhase.Live || Phase == RoundPhase.PostPlant) EvaluateEliminations();
        }

        /// <summary>
        /// The spike is down. This stops the round clock and starts the spike
        /// clock - after this point the round timer is irrelevant, which is the
        /// rule most implementations get wrong.
        /// </summary>
        public void ReportSpikePlanted()
        {
            if (Phase != RoundPhase.Live) return;

            _spikePlanted = true;
            Phase = RoundPhase.PostPlant;
            PhaseSecondsRemaining = _rules.SpikeSeconds;

            // Planting with the last attacker already dead is legal in principle
            // but cannot happen: a dead player cannot plant. Re-evaluating here
            // anyway costs nothing and covers a host that batches reports.
            EvaluateEliminations();
        }

        public void ReportSpikeDefused()
        {
            if (Phase != RoundPhase.PostPlant) return;
            EndRound(Side.Defenders, RoundWinReason.SpikeDefused);
        }

        public void Tick(float deltaSeconds)
        {
            if (!_started || IsMatchOver || deltaSeconds <= 0f) return;

            PhaseSecondsRemaining -= deltaSeconds;
            if (PhaseSecondsRemaining > 0f) return;

            switch (Phase)
            {
                case RoundPhase.Prep:
                    Phase = RoundPhase.Live;
                    PhaseSecondsRemaining = _rules.RoundSeconds;
                    break;

                case RoundPhase.Live:
                    // Time out with no plant is a defender win. If the spike were
                    // down we would be in PostPlant and never reach this.
                    EndRound(Side.Defenders, RoundWinReason.TimeExpired);
                    break;

                case RoundPhase.PostPlant:
                    EndRound(Side.Attackers, RoundWinReason.SpikeDetonated);
                    break;

                case RoundPhase.Over:
                    if (!IsMatchOver) BeginNextRound();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void EvaluateEliminations()
        {
            if (Phase == RoundPhase.PostPlant)
            {
                // With the spike down, wiping the attackers does NOT end the round -
                // the defenders still have to get to it and defuse. Wiping the
                // defenders does end it, because nobody is left who can.
                if (_defendersAlive == 0) EndRound(Side.Attackers, RoundWinReason.Elimination);
                return;
            }

            if (_attackersAlive == 0) EndRound(Side.Defenders, RoundWinReason.Elimination);
            else if (_defendersAlive == 0) EndRound(Side.Attackers, RoundWinReason.Elimination);
        }

        private void BeginNextRound()
        {
            // Swap before the round starts so spawns and the HUD are already
            // correct when players unfreeze.
            if (!SidesSwapped && RoundsPlayed == _rules.RoundsBeforeSwap && RoundsPlayed > 0)
            {
                SidesSwapped = true;
                HalftimeReached?.Invoke();
            }

            RoundNumber++;
            _spikePlanted = false;
            _attackersAlive = _rules.TeamSize;
            _defendersAlive = _rules.TeamSize;

            Phase = RoundPhase.Prep;
            PhaseSecondsRemaining = _rules.PrepSeconds;

            RoundStarted?.Invoke(RoundNumber);
        }

        private void EndRound(Side winningSide, RoundWinReason reason)
        {
            if (Phase == RoundPhase.Over) return;

            MatchTeam winningTeam = TeamOn(winningSide);

            if (winningTeam == MatchTeam.A) TeamAScore++;
            else TeamBScore++;

            RoundsPlayed++;
            Phase = RoundPhase.Over;
            PhaseSecondsRemaining = _rules.RoundOverSeconds;

            RoundEnded?.Invoke(new RoundResult(RoundNumber, winningSide, winningTeam, reason));

            if (ScoreOf(winningTeam) < _rules.RoundsToWin) return;

            IsMatchOver = true;
            Winner = winningTeam;
            MatchEnded?.Invoke(winningTeam);
        }
    }
}

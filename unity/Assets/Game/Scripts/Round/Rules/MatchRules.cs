namespace Game.Round.Rules
{
    /// <summary>
    /// Which half of the match a team is playing. Sides swap at halftime; team
    /// identity does not.
    ///
    /// Keeping side separate from team is the single most important modelling
    /// decision in here. Conflating them is why scoreboards break after the
    /// swap, and why "attackers won" gets credited to the wrong team.
    /// </summary>
    public enum Side
    {
        Attackers,
        Defenders
    }

    /// <summary>Persistent team identity for the whole match.</summary>
    public enum MatchTeam
    {
        None,
        A,
        B
    }

    public enum RoundPhase
    {
        /// <summary>Between rounds. Players are frozen at spawn.</summary>
        Prep,

        /// <summary>Round timer running, spike not planted.</summary>
        Live,

        /// <summary>Spike planted. The spike timer governs, not the round timer.</summary>
        PostPlant,

        /// <summary>Round decided, showing the result before the next Prep.</summary>
        Over
    }

    public enum RoundWinReason
    {
        None,
        Elimination,
        SpikeDetonated,
        SpikeDefused,
        TimeExpired
    }

    /// <summary>
    /// Match configuration. Plain data with no engine types so the rules can be
    /// compiled and tested outside Unity.
    /// </summary>
    public sealed class MatchRules
    {
        /// <summary>Round wins needed to take the match. 7 gives a ~25 minute game.</summary>
        public int RoundsToWin { get; set; } = 7;

        /// <summary>Players per team when full. Bots fill the empty slots.</summary>
        public int TeamSize { get; set; } = 5;

        /// <summary>Frozen-at-spawn time before each round goes live.</summary>
        public float PrepSeconds { get; set; } = 8f;

        /// <summary>Time attackers have to plant before defenders win on the clock.</summary>
        public float RoundSeconds { get; set; } = 100f;

        /// <summary>Time from plant to detonation.</summary>
        public float SpikeSeconds { get; set; } = 45f;

        /// <summary>How long the result stays on screen before the next round.</summary>
        public float RoundOverSeconds { get; set; } = 5f;

        /// <summary>
        /// Rounds played before sides swap. Valorant swaps after 12 of a
        /// first-to-13 match, so the general rule is RoundsToWin - 1.
        /// </summary>
        public int RoundsBeforeSwap => RoundsToWin - 1;

        /// <summary>Longest a match can run: both teams one short, then a decider.</summary>
        public int MaxRounds => RoundsToWin * 2 - 1;
    }

    /// <summary>How one round ended.</summary>
    public readonly struct RoundResult
    {
        /// <summary>1-based round number.</summary>
        public readonly int RoundNumber;

        /// <summary>The side that won.</summary>
        public readonly Side WinningSide;

        /// <summary>The team that was on that side, and therefore scored.</summary>
        public readonly MatchTeam WinningTeam;

        public readonly RoundWinReason Reason;

        public RoundResult(int roundNumber, Side winningSide, MatchTeam winningTeam, RoundWinReason reason)
        {
            RoundNumber = roundNumber;
            WinningSide = winningSide;
            WinningTeam = winningTeam;
            Reason = reason;
        }

        public override string ToString() =>
            $"Round {RoundNumber}: {WinningTeam} ({WinningSide}) by {Reason}";
    }
}

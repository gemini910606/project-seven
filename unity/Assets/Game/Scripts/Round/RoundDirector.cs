using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Game.Core;
using Game.Round.Rules;

namespace Game.Round
{
    /// <summary>
    /// Wires <see cref="MatchCore"/> into the scene and replicates its state.
    ///
    /// The host owns the match: only the server ticks the core, counts the
    /// living and decides rounds. Clients receive the results through
    /// NetworkVariables and never compute anything themselves. That is not an
    /// anti-cheat measure - among friends nobody is trying - it is how you avoid
    /// two machines disagreeing about who won.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundDirector : NetworkBehaviour
    {
        public static RoundDirector Instance { get; private set; }

        [Header("Match")]
        [SerializeField, Min(1)] private int roundsToWin = 7;
        [SerializeField, Min(1)] private int teamSize = 5;
        [SerializeField, Min(1f)] private float prepSeconds = 8f;
        [SerializeField, Min(10f)] private float roundSeconds = 100f;
        [SerializeField, Min(5f)] private float spikeSeconds = 45f;
        [SerializeField, Min(1f)] private float roundOverSeconds = 5f;

        [Header("Scene")]
        [SerializeField] private TeamSpawns spawns;
        [SerializeField] private Spike spike;

        private MatchCore _core;
        private readonly List<Health> _tracked = new();

        // Replicated state. Written by the server, read by everyone.
        private readonly NetworkVariable<int> _phase = new();
        private readonly NetworkVariable<float> _phaseRemaining = new();
        private readonly NetworkVariable<int> _roundNumber = new();
        private readonly NetworkVariable<int> _scoreA = new();
        private readonly NetworkVariable<int> _scoreB = new();
        private readonly NetworkVariable<bool> _sidesSwapped = new();

        public RoundPhase Phase => (RoundPhase)_phase.Value;
        public float SecondsRemaining => _phaseRemaining.Value;
        public int RoundNumber => _roundNumber.Value;
        public int ScoreA => _scoreA.Value;
        public int ScoreB => _scoreB.Value;

        /// <summary>Players per team when full. The bot director fills the gap.</summary>
        public int TeamSize => teamSize;

        /// <summary>True while players should be frozen at spawn.</summary>
        public bool PlayersFrozen => Phase == RoundPhase.Prep || Phase == RoundPhase.Over;

        /// <summary>
        /// Which side a team is on right now. Safe on clients: derived from the
        /// replicated swap flag rather than from the server-only core.
        ///
        /// Throws for <see cref="MatchTeam.None"/>, exactly as MatchCore does.
        /// This is the same rule stated twice - once in the tested core and once
        /// here for clients - and the two disagreeing is the whole failure mode
        /// the rules tests exist to catch. Returning Defenders for a teamless
        /// character, which is what this used to do, is a silent wrong answer.
        /// </summary>
        public Side SideOf(MatchTeam team)
        {
            if (team == MatchTeam.None)
            {
                throw new System.ArgumentException("No side for MatchTeam.None", nameof(team));
            }

            bool isTeamA = team == MatchTeam.A;
            bool attacking = _sidesSwapped.Value ? !isTeamA : isTeamA;
            return attacking ? Side.Attackers : Side.Defenders;
        }

        private void Awake() => Instance = this;

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            _core = new MatchCore(new MatchRules
            {
                RoundsToWin = roundsToWin,
                TeamSize = teamSize,
                PrepSeconds = prepSeconds,
                RoundSeconds = roundSeconds,
                SpikeSeconds = spikeSeconds,
                RoundOverSeconds = roundOverSeconds,
            });

            _core.RoundStarted += OnRoundStarted;
            _core.RoundEnded += OnRoundEnded;
            _core.MatchEnded += OnMatchEnded;

            _core.StartMatch();
            PublishState();
        }

        private void Update()
        {
            if (!IsServer || _core == null) return;

            _core.Tick(Time.deltaTime);
            PublishState();
        }

        // ------------------------------------------------------------------
        // Registration - characters tell the director they exist
        // ------------------------------------------------------------------

        /// <summary>Called by players and bots on spawn. Server-side only.</summary>
        public void Register(Health health)
        {
            if (!IsServer || health == null || _tracked.Contains(health)) return;

            _tracked.Add(health);
            health.Died += _ => RecountLiving();
            RecountLiving();
        }

        public void Unregister(Health health)
        {
            if (!IsServer) return;
            if (_tracked.Remove(health)) RecountLiving();
        }

        private void RecountLiving()
        {
            if (_core == null) return;

            int attackers = 0;
            int defenders = 0;

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                Health health = _tracked[i];
                if (health == null)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                if (!health.IsAlive) continue;
                if (!health.TryGetComponent(out TeamMember member)) continue;

                // A character with no team is on neither side, so it is not part
                // of either count. Asking MatchCore instead throws, and this loop
                // is what decides when a round ends by elimination - an exception
                // here stops rounds ending at all.
                if (member.Team == MatchTeam.None) continue;

                if (_core.SideOf(member.Team) == Side.Attackers) attackers++;
                else defenders++;
            }

            _core.ReportAlive(Side.Attackers, attackers);
            _core.ReportAlive(Side.Defenders, defenders);
        }

        // ------------------------------------------------------------------
        // Spike
        // ------------------------------------------------------------------

        public void NotifySpikePlanted()
        {
            if (!IsServer) return;
            _core?.ReportSpikePlanted();
            PublishState();
        }

        public void NotifySpikeDefused()
        {
            if (!IsServer) return;
            _core?.ReportSpikeDefused();
            PublishState();
        }

        // ------------------------------------------------------------------
        // Server -> client
        // ------------------------------------------------------------------

        private void PublishState()
        {
            _phase.Value = (int)_core.Phase;
            _phaseRemaining.Value = _core.PhaseSecondsRemaining;
            _roundNumber.Value = _core.RoundNumber;
            _scoreA.Value = _core.TeamAScore;
            _scoreB.Value = _core.TeamBScore;
            _sidesSwapped.Value = _core.SidesSwapped;
        }

        private void OnRoundStarted(int roundNumber)
        {
            // Everyone comes back alive at their side's spawn. Respawning is the
            // round boundary; there is no respawn inside a round.
            spike?.ResetForRound();
            spawns?.ResetCursors();

            foreach (Health health in _tracked)
            {
                if (health == null) continue;

                health.ResetToFull();
                health.gameObject.SetActive(true);

                if (spawns != null
                    && health.TryGetComponent(out TeamMember member)
                    && member.Team != MatchTeam.None)
                {
                    spawns.PlaceAtSpawn(health.transform, _core.SideOf(member.Team));
                }

                // Bodies keep their hitboxes switched off from the moment they
                // died until here.
                if (health.TryGetComponent(out CharacterHitboxes hitboxes)) hitboxes.ResetForRound();

                if (health.TryGetComponent(out Game.Bots.BotBrain brain)) brain.ResetForRound();
            }

            RecountLiving();
            RoundStartedClientRpc(roundNumber);
        }

        private void OnRoundEnded(RoundResult result) =>
            RoundEndedClientRpc(result.RoundNumber, (int)result.WinningTeam, (int)result.Reason);

        private void OnMatchEnded(MatchTeam winner) => MatchEndedClientRpc((int)winner);

        [ClientRpc]
        private void RoundStartedClientRpc(int roundNumber) => RoundStarted?.Invoke(roundNumber);

        [ClientRpc]
        private void RoundEndedClientRpc(int roundNumber, int winningTeam, int reason) =>
            RoundEnded?.Invoke(roundNumber, (MatchTeam)winningTeam, (RoundWinReason)reason);

        [ClientRpc]
        private void MatchEndedClientRpc(int winner) => MatchEnded?.Invoke((MatchTeam)winner);

        /// <summary>Raised on every machine when a round begins.</summary>
        public event System.Action<int> RoundStarted;

        /// <summary>Raised on every machine with (roundNumber, winningTeam, reason).</summary>
        public event System.Action<int, MatchTeam, RoundWinReason> RoundEnded;

        public event System.Action<MatchTeam> MatchEnded;
    }
}

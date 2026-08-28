using UnityEngine;
using Game.Core;
using Game.Round;
using Game.Round.Rules;

namespace Game.Bots
{
    public enum BotState
    {
        Idle,
        Advance,
        Engage,
        Search,
        Interact,
        Dead
    }

    /// <summary>
    /// A bot that plays the round rather than guarding a warehouse.
    ///
    /// This replaced an open-world enemy AI, and the change of job is bigger than
    /// it looks. The old brain patrolled, investigated noises and escalated a
    /// wanted level; those are behaviours for a world that persists. A round-based
    /// bot has exactly one job that changes every round: attackers go plant,
    /// defenders go hold, and both fight whatever they meet on the way.
    ///
    /// Bots exist so five-versus-five works with three friends online. They run
    /// on the host only and are replicated like any other character - a client
    /// cannot tell a bot from a laggy human, which is the point.
    ///
    /// Difficulty lives in BotWeaponUser (reaction delay, burst rhythm, aim
    /// error), never in giving bots better numbers than players get.
    /// </summary>
    [RequireComponent(typeof(BotPerception))]
    [RequireComponent(typeof(BotLocomotion))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(TeamMember))]
    [DisallowMultipleComponent]
    public sealed class BotBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BotWeaponUser weaponUser;

        [Header("Combat")]
        [Tooltip("Preferred distance to the target, in metres.")]
        [SerializeField, Min(1f)] private float engagementRange = 16f;

        [Tooltip("Closer than this and the bot backs off instead of hugging its target.")]
        [SerializeField, Min(0.5f)] private float minimumRange = 5f;

        [Tooltip("Seconds between repositioning during a fight. Constant strafing looks robotic.")]
        [SerializeField, Min(0.1f)] private float repositionInterval = 2.6f;

        [Header("Objective")]
        [Tooltip("How close counts as being on the objective, in metres.")]
        [SerializeField, Min(0.5f)] private float objectiveRadius = 2f;

        [Tooltip("Defenders hold a position this far from the site rather than standing on it.")]
        [SerializeField, Min(0f)] private float holdOffset = 7f;

        [Header("Search")]
        [SerializeField, Min(0f)] private float searchDuration = 8f;
        [SerializeField, Min(1f)] private float searchRadius = 7f;
        [SerializeField, Min(0.5f)] private float searchPointInterval = 2.5f;

        [Header("Retargeting")]
        [Tooltip("Seconds between looking for a better target. Per-frame is waste; ten characters do not move that fast.")]
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.4f;

        private BotPerception _perception;
        private BotLocomotion _locomotion;
        private Health _health;
        private TeamMember _team;

        private BotState _state = BotState.Idle;
        private float _stateEnteredTime;
        private float _nextActionTime;
        private float _nextRetargetTime;
        private Transform _objective;

        public BotState State => _state;

        private void Awake()
        {
            _perception = GetComponent<BotPerception>();
            _locomotion = GetComponent<BotLocomotion>();
            _health = GetComponent<Health>();
            _team = GetComponent<TeamMember>();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
            NoiseSystem.Instance?.Register(_perception);
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
            NoiseSystem.Instance?.Unregister(_perception);
        }

        /// <summary>Called by the bot director with the point this bot is playing for.</summary>
        public void SetObjective(Transform objective) => _objective = objective;

        private void Update()
        {
            if (_state == BotState.Dead) return;

            RoundDirector director = RoundDirector.Instance;

            // Frozen during prep and after the round is decided. A bot that keeps
            // running around while the scoreboard is up looks broken.
            if (director != null && director.PlayersFrozen)
            {
                if (_state != BotState.Idle) TransitionTo(BotState.Idle);
                _locomotion.Stop();
                return;
            }

            Retarget();

            switch (_state)
            {
                case BotState.Idle: TickIdle(); break;
                case BotState.Advance: TickAdvance(director); break;
                case BotState.Engage: TickEngage(); break;
                case BotState.Search: TickSearch(); break;
                case BotState.Interact: TickInteract(director); break;
            }
        }

        // ------------------------------------------------------------------
        // Targeting
        // ------------------------------------------------------------------

        private void Retarget()
        {
            if (Time.time < _nextRetargetTime) return;
            _nextRetargetTime = Time.time + retargetInterval;

            // Keep a confirmed target rather than flip-flopping between two
            // enemies at similar range, which makes a bot look indecisive and
            // stops it ever finishing a burst.
            if (_perception.HasConfirmedTarget && _perception.HasLineOfSight) return;

            TeamMember hostile = Combatants.NearestHostile(_team, transform.position);
            _perception.SetTarget(hostile != null ? hostile.transform : null);
        }

        // ------------------------------------------------------------------
        // States
        // ------------------------------------------------------------------

        private void TickIdle()
        {
            if (_perception.HasConfirmedTarget) { TransitionTo(BotState.Engage); return; }
            TransitionTo(BotState.Advance);
        }

        private void TickAdvance(RoundDirector director)
        {
            if (_perception.HasConfirmedTarget) { TransitionTo(BotState.Engage); return; }

            if (_objective == null) return;

            Vector3 goal = ObjectiveGoal(director);

            if (Vector3.Distance(transform.position, goal) <= objectiveRadius)
            {
                // Attackers standing on the site with the spike should plant it;
                // defenders standing on a planted spike should defuse it.
                if (WantsToInteract(director)) { TransitionTo(BotState.Interact); return; }

                _locomotion.Stop();
                return;
            }

            if (Time.time < _nextActionTime) return;
            _nextActionTime = Time.time + 1f;

            _locomotion.SetPace(BotPace.Investigate);
            _locomotion.MoveTo(goal);
        }

        private void TickEngage()
        {
            if (!_perception.HasConfirmedTarget && !_perception.HasRecentMemory)
            {
                TransitionTo(BotState.Search);
                return;
            }

            Transform target = _perception.Target;
            if (target == null) { TransitionTo(BotState.Search); return; }

            Vector3 targetPosition = _perception.HasLineOfSight ? target.position : _perception.LastKnownPosition;
            _locomotion.FaceTowards(targetPosition);

            if (_perception.HasLineOfSight && weaponUser != null) weaponUser.EngageTarget(target);

            if (Time.time < _nextActionTime) return;
            _nextActionTime = Time.time + repositionInterval;

            float distance = Vector3.Distance(transform.position, targetPosition);

            if (distance > engagementRange) _locomotion.MoveTo(targetPosition);
            else if (distance < minimumRange)
            {
                Vector3 away = (transform.position - targetPosition).normalized;
                _locomotion.MoveTo(transform.position + away * (minimumRange - distance + 2f));
            }
            else if (_locomotion.TryFindPointNear(transform.position, 3.5f, out Vector3 flank))
            {
                _locomotion.MoveTo(flank);
            }
        }

        private void TickSearch()
        {
            if (_perception.HasConfirmedTarget) { TransitionTo(BotState.Engage); return; }

            // Searching is bounded here, unlike the open-world version: a round
            // has a clock, and a bot that hunts forever never gets to the site.
            if (Time.time - _stateEnteredTime > searchDuration)
            {
                TransitionTo(BotState.Advance);
                return;
            }

            if (Time.time < _nextActionTime) return;
            _nextActionTime = Time.time + searchPointInterval;

            if (_locomotion.TryFindPointNear(_perception.LastKnownPosition, searchRadius, out Vector3 point))
            {
                _locomotion.MoveTo(point);
            }
        }

        private void TickInteract(RoundDirector director)
        {
            // Being shot at while planting is the interesting decision. Bots take
            // the same one a player would: stop and fight.
            if (_perception.HasConfirmedTarget && _perception.HasLineOfSight)
            {
                TransitionTo(BotState.Engage);
                return;
            }

            if (!WantsToInteract(director)) { TransitionTo(BotState.Advance); return; }

            Spike spike = FindSpike();
            if (spike == null) { TransitionTo(BotState.Advance); return; }

            _locomotion.Stop();
            _locomotion.FaceTowards(spike.transform.position);
            spike.BotInteract(gameObject);
        }

        // ------------------------------------------------------------------
        // Objective helpers
        // ------------------------------------------------------------------

        private Vector3 ObjectiveGoal(RoundDirector director)
        {
            if (_objective == null) return transform.position;

            bool attacking = director != null && director.SideOf(_team.Team) == Side.Attackers;
            if (attacking || holdOffset <= 0f) return _objective.position;

            // Defenders hold short of the site rather than standing on it, so
            // attackers have to take the space instead of walking into a huddle.
            Vector3 away = (transform.position - _objective.position);
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;

            return _objective.position + away.normalized * holdOffset;
        }

        private bool WantsToInteract(RoundDirector director)
        {
            if (director == null) return false;

            Spike spike = FindSpike();
            if (spike == null) return false;

            Side side = director.SideOf(_team.Team);

            if (side == Side.Attackers)
                return !spike.IsPlanted && director.Phase == RoundPhase.Live;

            return spike.IsPlanted && director.Phase == RoundPhase.PostPlant;
        }

        private Spike FindSpike() => Object.FindFirstObjectByType<Spike>();

        // ------------------------------------------------------------------
        // Plumbing
        // ------------------------------------------------------------------

        private void TransitionTo(BotState next)
        {
            if (_state == next) return;

            _state = next;
            _stateEnteredTime = Time.time;
            _nextActionTime = 0f;

            switch (next)
            {
                case BotState.Idle:
                case BotState.Interact:
                    _locomotion.Stop();
                    weaponUser?.HoldFire();
                    break;

                case BotState.Advance:
                    _locomotion.SetPace(BotPace.Investigate);
                    weaponUser?.HoldFire();
                    break;

                case BotState.Engage:
                    _locomotion.SetPace(BotPace.Combat);
                    break;

                case BotState.Search:
                    _locomotion.SetPace(BotPace.Investigate);
                    weaponUser?.HoldFire();
                    break;

                case BotState.Dead:
                    _locomotion.Stop();
                    weaponUser?.HoldFire();
                    break;
            }
        }

        /// <summary>Called by the director at the start of each round.</summary>
        public void ResetForRound()
        {
            _state = BotState.Idle;
            _perception.SetTarget(null);
            if (weaponUser != null) weaponUser.ResetForRound();
            enabled = true;
        }

        private void OnDied(DamageInfo info)
        {
            TransitionTo(BotState.Dead);
            enabled = false;
        }
    }
}

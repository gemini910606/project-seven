using UnityEngine;
using Game.Core;

namespace Game.AI
{
    public enum EnemyState
    {
        Idle,
        Patrol,
        Investigate,
        Combat,
        Search,
        Dead
    }

    /// <summary>
    /// The enemy state machine.
    ///
    /// A plain switch rather than a behaviour tree package. At this size a tree
    /// is more machinery than behaviour: six states with explicit transitions fit
    /// on one screen and can be reasoned about without a graph editor. Reach for
    /// a tree when the state count passes roughly fifteen, not before.
    ///
    /// The Search state is what makes the AI feel alive. Losing the player and
    /// then hunting for them - rather than instantly forgetting or instantly
    /// knowing - is the difference between guards and turrets.
    /// </summary>
    [RequireComponent(typeof(EnemyPerception))]
    [RequireComponent(typeof(EnemyLocomotion))]
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class EnemyBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PatrolRoute patrolRoute;
        [SerializeField] private EnemyWeaponUser weaponUser;

        [Header("Combat")]
        [Tooltip("Preferred distance to the target, in metres.")]
        [SerializeField, Min(1f)] private float engagementRange = 14f;

        [Tooltip("Closer than this and the enemy backs off instead of hugging the player.")]
        [SerializeField, Min(0.5f)] private float minimumRange = 6f;

        [Tooltip("Seconds between repositioning while in combat. Constant strafing looks robotic.")]
        [SerializeField, Min(0.1f)] private float repositionInterval = 3.2f;

        [Header("Search")]
        [Tooltip("Seconds spent hunting after losing the target before giving up.")]
        [SerializeField, Min(0f)] private float searchDuration = 14f;

        [Tooltip("Radius around the last known position that gets searched.")]
        [SerializeField, Min(1f)] private float searchRadius = 9f;

        [SerializeField, Min(0.5f)] private float searchPointInterval = 3f;

        private EnemyPerception _perception;
        private EnemyLocomotion _locomotion;
        private Health _health;
        private AlertSystem _alert;

        private EnemyState _state = EnemyState.Idle;
        private float _stateEnteredTime;
        private float _nextActionTime;
        private int _waypointIndex;
        private int _waypointDirection = 1;
        private bool _countedAsWitness;

        public EnemyState State => _state;

        private void Awake()
        {
            _perception = GetComponent<EnemyPerception>();
            _locomotion = GetComponent<EnemyLocomotion>();
            _health = GetComponent<Health>();
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
            ReportWitness(false);
        }

        /// <summary>Wired by the spawner so the enemy does not go looking for the player itself.</summary>
        public void Initialise(Transform target, AlertSystem alertSystem)
        {
            _perception.SetTarget(target);
            _alert = alertSystem;
            TransitionTo(patrolRoute != null && patrolRoute.Count > 0 ? EnemyState.Patrol : EnemyState.Idle);
        }

        private void Update()
        {
            if (_state == EnemyState.Dead) return;

            SyncWitnessCount();

            switch (_state)
            {
                case EnemyState.Idle: TickIdle(); break;
                case EnemyState.Patrol: TickPatrol(); break;
                case EnemyState.Investigate: TickInvestigate(); break;
                case EnemyState.Combat: TickCombat(); break;
                case EnemyState.Search: TickSearch(); break;
            }
        }

        // ------------------------------------------------------------------
        // States
        // ------------------------------------------------------------------

        private void TickIdle()
        {
            if (_perception.HasConfirmedTarget) { TransitionTo(EnemyState.Combat); return; }
            if (_perception.Awareness > 0.1f) TransitionTo(EnemyState.Investigate);
        }

        private void TickPatrol()
        {
            if (_perception.HasConfirmedTarget) { TransitionTo(EnemyState.Combat); return; }
            if (_perception.Awareness > 0.1f) { TransitionTo(EnemyState.Investigate); return; }

            if (patrolRoute == null || patrolRoute.Count == 0) { TransitionTo(EnemyState.Idle); return; }

            if (_locomotion.HasArrived && Time.time >= _nextActionTime)
            {
                _waypointIndex = patrolRoute.NextIndex(_waypointIndex, ref _waypointDirection);
                _locomotion.MoveTo(patrolRoute.PositionAt(_waypointIndex));
                _nextActionTime = Time.time + patrolRoute.WaitSeconds;
            }
        }

        private void TickInvestigate()
        {
            if (_perception.HasConfirmedTarget) { TransitionTo(EnemyState.Combat); return; }

            // Awareness decayed to nothing: it was a rat, go back to work.
            if (_perception.Awareness <= 0.01f)
            {
                TransitionTo(patrolRoute != null && patrolRoute.Count > 0 ? EnemyState.Patrol : EnemyState.Idle);
                return;
            }

            if (_locomotion.HasArrived) _locomotion.FaceTowards(_perception.LastKnownPosition);
            else if (Time.time >= _nextActionTime)
            {
                _locomotion.MoveTo(_perception.LastKnownPosition);
                _nextActionTime = Time.time + 1f;
            }
        }

        private void TickCombat()
        {
            if (!_perception.HasConfirmedTarget && !_perception.HasRecentMemory)
            {
                TransitionTo(EnemyState.Search);
                return;
            }

            Transform target = _perception.Target;
            if (target == null) { TransitionTo(EnemyState.Search); return; }

            Vector3 targetPosition = _perception.HasLineOfSight ? target.position : _perception.LastKnownPosition;
            _locomotion.FaceTowards(targetPosition);

            if (_perception.HasLineOfSight && weaponUser != null)
            {
                weaponUser.EngageTarget(target);
            }

            if (Time.time < _nextActionTime) return;
            _nextActionTime = Time.time + repositionInterval;

            float distance = Vector3.Distance(transform.position, targetPosition);

            if (distance > engagementRange)
            {
                _locomotion.MoveTo(targetPosition);
            }
            else if (distance < minimumRange)
            {
                // Back off along the line away from the target rather than
                // strafing, which keeps the enemy facing the player while it moves.
                Vector3 away = (transform.position - targetPosition).normalized;
                _locomotion.MoveTo(transform.position + away * (minimumRange - distance + 2f));
            }
            else if (_locomotion.TryFindPointNear(transform.position, 4f, out Vector3 flank))
            {
                _locomotion.MoveTo(flank);
            }
        }

        private void TickSearch()
        {
            if (_perception.HasConfirmedTarget) { TransitionTo(EnemyState.Combat); return; }

            if (Time.time - _stateEnteredTime > searchDuration)
            {
                TransitionTo(patrolRoute != null && patrolRoute.Count > 0 ? EnemyState.Patrol : EnemyState.Idle);
                return;
            }

            if (Time.time < _nextActionTime) return;
            _nextActionTime = Time.time + searchPointInterval;

            if (_locomotion.TryFindPointNear(_perception.LastKnownPosition, searchRadius, out Vector3 point))
            {
                _locomotion.MoveTo(point);
            }
        }

        // ------------------------------------------------------------------
        // Plumbing
        // ------------------------------------------------------------------

        private void TransitionTo(EnemyState next)
        {
            if (_state == next) return;

            _state = next;
            _stateEnteredTime = Time.time;
            _nextActionTime = 0f;

            switch (next)
            {
                case EnemyState.Idle:
                    _locomotion.Stop();
                    weaponUser?.HoldFire();
                    break;

                case EnemyState.Patrol:
                    _locomotion.SetPace(EnemyPace.Patrol);
                    weaponUser?.HoldFire();
                    break;

                case EnemyState.Investigate:
                    _locomotion.SetPace(EnemyPace.Investigate);
                    weaponUser?.HoldFire();
                    break;

                case EnemyState.Combat:
                    _locomotion.SetPace(EnemyPace.Combat);
                    // Seeing the player is what actually raises the city's heat.
                    _alert?.ReportPlayerSpotted();
                    break;

                case EnemyState.Search:
                    _locomotion.SetPace(EnemyPace.Investigate);
                    weaponUser?.HoldFire();
                    break;

                case EnemyState.Dead:
                    _locomotion.Stop();
                    weaponUser?.HoldFire();
                    ReportWitness(false);
                    break;
            }
        }

        /// <summary>
        /// The alert system needs to know how many enemies can currently see the
        /// player so it knows when to start cooling down. Tracking the edge here
        /// keeps the count balanced even when the state machine skips states.
        /// </summary>
        private void SyncWitnessCount()
        {
            bool isWitness = _state == EnemyState.Combat && _perception.HasLineOfSight;
            if (isWitness != _countedAsWitness) ReportWitness(isWitness);
        }

        private void ReportWitness(bool visible)
        {
            if (_countedAsWitness == visible) return;
            _countedAsWitness = visible;
            _alert?.SetWitnessVisible(visible);
        }

        private void OnDied(DamageInfo info)
        {
            TransitionTo(EnemyState.Dead);
            _alert?.ReportKill();
            enabled = false;
        }
    }
}

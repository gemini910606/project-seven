using UnityEngine;
using UnityEngine.AI;

namespace Game.Bots
{
    /// <summary>
    /// NavMeshAgent wrapper. Keeps pathing details out of the brain so the state
    /// machine reads as intent ("go here", "face this") rather than as agent API.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class BotLocomotion : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float patrolSpeed = 1.9f;
        [SerializeField, Min(0f)] private float investigateSpeed = 3.4f;
        [SerializeField, Min(0f)] private float combatSpeed = 4.6f;

        [Tooltip("How close counts as arrived, in metres.")]
        [SerializeField, Min(0.1f)] private float arrivalTolerance = 0.6f;

        [Tooltip("Degrees per second when turning to face a target while stationary.")]
        [SerializeField, Min(1f)] private float facingTurnSpeed = 480f;

        private NavMeshAgent _agent;

        public float Speed => _agent != null ? _agent.velocity.magnitude : 0f;

        /// <summary>Speed as a fraction of the combat speed, for animator blend trees.</summary>
        public float NormalizedSpeed => combatSpeed > 0f ? Speed / combatSpeed : 0f;

        public bool HasArrived =>
            _agent != null
            && !_agent.pathPending
            && _agent.remainingDistance <= Mathf.Max(arrivalTolerance, _agent.stoppingDistance);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.stoppingDistance = arrivalTolerance;
        }

        public void SetPace(BotPace pace)
        {
            if (_agent == null) return;
            _agent.speed = pace switch
            {
                BotPace.Patrol => patrolSpeed,
                BotPace.Investigate => investigateSpeed,
                _ => combatSpeed
            };
        }

        /// <summary>
        /// Moves towards a world point, snapping it onto the NavMesh first.
        /// SetDestination on an off-mesh point silently fails and the enemy just
        /// stands there, which is maddening to debug.
        /// </summary>
        public bool MoveTo(Vector3 worldPoint, float snapRadius = 3f)
        {
            if (_agent == null || !_agent.isOnNavMesh) return false;

            if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, snapRadius, NavMesh.AllAreas))
            {
                return false;
            }

            _agent.isStopped = false;
            return _agent.SetDestination(hit.position);
        }

        public void Stop()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        /// <summary>Turns the body to face a point without moving. Used while shooting from cover.</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 delta = worldPoint - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(delta),
                facingTurnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Finds a reachable point roughly `radius` away, for search behaviour.
        /// Returns false when the sample fails so the caller can pick again
        /// rather than walking to (0,0,0).
        /// </summary>
        public bool TryFindPointNear(Vector3 centre, float radius, out Vector3 point)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = centre + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }

            point = centre;
            return false;
        }
    }

    public enum BotPace
    {
        Patrol,
        Investigate,
        Combat
    }
}

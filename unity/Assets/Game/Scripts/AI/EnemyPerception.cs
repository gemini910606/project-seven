using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Sight and hearing for one enemy.
    ///
    /// Sight is a cone plus a line-of-sight check plus a time-to-notice delay.
    /// The delay is the important part: instant detection makes stealth feel
    /// arbitrary and unfair, whereas a visible second of "have they seen me?"
    /// turns every corner into a decision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPerception : MonoBehaviour
    {
        [Header("Sight")]
        [SerializeField, Min(0f)] private float viewDistance = 32f;
        [SerializeField, Range(1f, 180f)] private float viewConeDegrees = 105f;

        [Tooltip("Distance within which the player is noticed regardless of facing. Stops enemies being deaf and blind to someone standing behind them.")]
        [SerializeField, Min(0f)] private float proximityRadius = 4f;

        [Tooltip("Eye position. Raycasting from the feet sees through waist-high cover.")]
        [SerializeField] private Transform eyes;

        [Tooltip("Everything that blocks line of sight. Must not include the player's own layer.")]
        [SerializeField] private LayerMask occlusionMask = ~0;

        [Header("Awareness")]
        [Tooltip("Seconds of continuous visibility at maximum range before the target is confirmed. Closer targets are confirmed faster.")]
        [SerializeField, Min(0.05f)] private float timeToNotice = 1.1f;

        [Tooltip("Seconds of not seeing the target before awareness starts draining.")]
        [SerializeField, Min(0f)] private float memoryDuration = 6f;

        [Header("Hearing")]
        [SerializeField, Min(0f)] private float hearingMultiplier = 1f;

        [Header("Scanning")]
        [Tooltip("How often the line-of-sight raycast runs, in seconds. Perception does not need to be per-frame and this is the single biggest AI cost.")]
        [SerializeField, Min(0.02f)] private float scanInterval = 0.15f;

        private Transform _target;
        private float _awareness;
        private float _lastSeenTime = float.NegativeInfinity;
        private float _nextScanTime;

        /// <summary>0..1. Reaches 1 when the target is confirmed.</summary>
        public float Awareness => _awareness;

        public bool HasConfirmedTarget => _awareness >= 1f;

        /// <summary>True while the target is in the cone and unobstructed right now.</summary>
        public bool HasLineOfSight { get; private set; }

        /// <summary>Last position the target was actually seen or heard at.</summary>
        public Vector3 LastKnownPosition { get; private set; }

        public bool HasRecentMemory => Time.time - _lastSeenTime <= memoryDuration;

        public Transform Target => _target;

        private Vector3 EyePosition => eyes != null ? eyes.position : transform.position + Vector3.up * 1.6f;

        public void SetTarget(Transform target) => _target = target;

        private void Update()
        {
            if (_target == null) return;

            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + scanInterval;
                HasLineOfSight = CanSee(_target.position, out float distance);

                if (HasLineOfSight)
                {
                    _lastSeenTime = Time.time;
                    LastKnownPosition = _target.position;

                    // Notice faster up close. A silhouette at 30m taking a full
                    // second is fine; the same delay at 3m reads as broken AI.
                    float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, viewDistance));
                    float rate = Mathf.Lerp(1f, 3.5f, closeness) / timeToNotice;
                    _awareness = Mathf.Min(1f, _awareness + rate * scanInterval);
                }
            }

            if (!HasLineOfSight && !HasRecentMemory)
            {
                _awareness = Mathf.Max(0f, _awareness - Time.deltaTime / Mathf.Max(0.01f, memoryDuration));
            }
        }

        private bool CanSee(Vector3 point, out float distance)
        {
            Vector3 eye = EyePosition;
            Vector3 delta = point + Vector3.up * 1.0f - eye;
            distance = delta.magnitude;

            if (distance > viewDistance) return false;

            if (distance > proximityRadius)
            {
                float angle = Vector3.Angle(transform.forward, delta);
                if (angle > viewConeDegrees * 0.5f) return false;
            }

            // A hit before the target means something is in the way.
            return !Physics.Raycast(
                eye, delta.normalized, distance - 0.1f, occlusionMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Called by the noise system. Loud things nearby are as good as being
        /// seen; quiet things only give a direction to look in.
        /// </summary>
        public void HearNoise(Vector3 position, float radius)
        {
            float distance = Vector3.Distance(EyePosition, position);
            float effectiveRadius = radius * hearingMultiplier;
            if (distance > effectiveRadius) return;

            LastKnownPosition = position;
            _lastSeenTime = Time.time;

            float loudness = 1f - (distance / Mathf.Max(0.01f, effectiveRadius));
            _awareness = Mathf.Min(1f, _awareness + loudness * 0.6f);
        }

        /// <summary>Instantly confirms the target. For scripted alarms and cameras.</summary>
        public void ForceAlert(Vector3 position)
        {
            LastKnownPosition = position;
            _lastSeenTime = Time.time;
            _awareness = 1f;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eye = EyePosition;

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(eye, viewDistance);

            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(eye, proximityRadius);

            Vector3 left = Quaternion.Euler(0f, -viewConeDegrees * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, viewConeDegrees * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(eye, eye + left * viewDistance);
            Gizmos.DrawLine(eye, eye + right * viewDistance);
        }
    }
}

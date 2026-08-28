using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// The accuracy model, as pure maths so it can be unit-tested and so the AI
    /// can use the same numbers the player does without dragging a MonoBehaviour
    /// along. Held by value inside <see cref="Weapon"/>.
    /// </summary>
    public struct WeaponSpread
    {
        private float _accumulated;

        /// <summary>Spread added by sustained fire, in degrees.</summary>
        public readonly float Accumulated => _accumulated;

        public void RegisterShot(WeaponDefinition def)
        {
            _accumulated = Mathf.Min(def.MaxSpreadDegrees, _accumulated + def.SpreadPerShot);
        }

        public void Recover(WeaponDefinition def, float deltaTime)
        {
            _accumulated = Mathf.Max(0f, _accumulated - def.SpreadRecoveryPerSecond * deltaTime);
        }

        public void Reset() => _accumulated = 0f;

        /// <summary>
        /// Total cone half-angle for the next shot. Aiming, standing still and
        /// short bursts are all rewarded, which is what makes a gunfight about
        /// positioning rather than about holding the trigger.
        /// </summary>
        public readonly float CurrentDegrees(WeaponDefinition def, bool aiming, float moveSpeed)
        {
            float spread = def.BaseSpreadDegrees + _accumulated + moveSpeed * def.SpreadPerMoveSpeed;
            if (!aiming) spread += def.HipFireSpreadDegrees;
            return Mathf.Min(spread, def.MaxSpreadDegrees + def.HipFireSpreadDegrees);
        }

        /// <summary>
        /// Perturbs a direction by a random angle inside the cone.
        /// </summary>
        /// <remarks>
        /// Uses a uniform point on the disc (sqrt of a uniform sample) rather than
        /// a uniform radius. A uniform radius clusters shots in the middle, which
        /// makes a "wide" cone feel accurate and the tuning numbers meaningless.
        /// </remarks>
        public static Vector3 Apply(Vector3 direction, float coneHalfAngleDegrees)
        {
            if (coneHalfAngleDegrees <= 0f) return direction;

            float maxRadians = coneHalfAngleDegrees * Mathf.Deg2Rad;
            float radius = Mathf.Tan(maxRadians) * Mathf.Sqrt(Random.value);
            float theta = Random.value * Mathf.PI * 2f;

            Vector3 forward = direction.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // Straight up or down leaves the cross product degenerate.
            if (right.sqrMagnitude < 0.0001f) right = Vector3.Cross(Vector3.forward, forward);
            right.Normalize();

            Vector3 up = Vector3.Cross(forward, right);

            Vector3 offset = right * (Mathf.Cos(theta) * radius) + up * (Mathf.Sin(theta) * radius);
            return (forward + offset).normalized;
        }
    }
}

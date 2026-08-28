using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Everything a damage event needs to be resolved, scored and reacted to.
    /// Passed by value; it is deliberately a struct so firing a full-auto weapon
    /// does not allocate once per bullet.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;

        /// <summary>Who caused this. Null for world damage such as a fall.</summary>
        public readonly GameObject Instigator;

        /// <summary>True when the hit landed on a collider tagged as a weak point.</summary>
        public readonly bool IsCritical;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject instigator, bool isCritical = false)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Instigator = instigator;
            IsCritical = isCritical;
        }
    }
}

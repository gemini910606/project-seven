using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Finds the <see cref="IDamageable"/> that owns a collider.
    /// </summary>
    public static class DamageResolver
    {
        /// <summary>
        /// A character is a hierarchy of colliders (hitboxes, ragdoll bones) with
        /// one Health component near the root. Checking the attached rigidbody
        /// first is the cheap path; the parent walk is the fallback for colliders
        /// that have no rigidbody of their own.
        /// </summary>
        public static IDamageable Resolve(Collider collider)
        {
            if (collider == null) return null;

            var body = collider.attachedRigidbody;
            if (body != null && body.TryGetComponent(out IDamageable fromBody)) return fromBody;

            return collider.GetComponentInParent<IDamageable>();
        }
    }
}

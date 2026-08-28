using UnityEngine;
using Game.Core;

namespace Game.Weapons
{
    /// <summary>
    /// Traces a shot and applies damage. **Server-side only.**
    ///
    /// This is the single place in the game where health changes as a result of
    /// a bullet. Keeping it in one server-side component is what stops a kill
    /// existing on one machine and not the others.
    ///
    /// There is no lag compensation. The server traces against where characters
    /// are *now*, not where the shooter saw them, so a client with 60ms of ping
    /// has to lead a running target very slightly. Doing this properly means
    /// rewinding every hitbox to the shooter's timestamp, which is a large piece
    /// of work and buys nothing among friends on a decent connection. If it ever
    /// starts to feel wrong, measure the ping before writing any code.
    /// </summary>
    public sealed class ShotResolver : MonoBehaviour
    {
        [Tooltip("Everything a bullet can hit. Exclude the shooter's own layer or every shot hits the barrel of the gun.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Tooltip("Colliders on this layer count as weak points and deal critical damage.")]
        [SerializeField] private LayerMask weakPointMask;

        [SerializeField] private GameObject impactEffect;

        /// <summary>
        /// Traces one pellet and applies its damage.
        /// </summary>
        /// <param name="shooter">Used to attribute the kill and to skip friendly fire.</param>
        /// <returns>True if something damageable was hit.</returns>
        public bool Resolve(GameObject shooter, WeaponDefinition definition, Vector3 origin, Vector3 direction)
        {
            if (definition == null) return false;

            if (!Physics.Raycast(origin, direction, out RaycastHit hit,
                    definition.Range, hitMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (impactEffect != null)
            {
                Destroy(Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal)), 3f);
            }

            IDamageable target = DamageResolver.Resolve(hit.collider);
            if (target == null || !target.IsAlive) return false;

            // No friendly fire. In a five-a-side game with bots it produces far
            // more frustration than tension, and a bot that shoots through a
            // teammate is a bug report waiting to happen.
            if (!TeamMember.AreHostile(shooter, hit.collider.gameObject)) return false;

            bool critical = (weakPointMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            float damage = definition.DamageAtDistance(hit.distance);

            target.ApplyDamage(new DamageInfo(damage, hit.point, direction, shooter, critical));
            return true;
        }
    }
}

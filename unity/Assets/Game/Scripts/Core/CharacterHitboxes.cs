using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Switches a character's hitboxes off when it dies and back on when the
    /// round resets it.
    ///
    /// Without this, corpses absorb bullets. Nothing removes a dead character
    /// from the world - there is no respawn inside a round, so bodies accumulate
    /// until the round ends - and their hitboxes stay on the layers ShotResolver
    /// traces against. A raycast stops at the first collider it meets, so the
    /// shot resolves against a dead Health, finds IsAlive false, and returns
    /// having done nothing. The bullet is spent. By the end of a round a doorway
    /// can hold several bulletproof bodies.
    ///
    /// Only the hitbox layers are touched. The CharacterController lives on the
    /// root on Ignore Raycast and is left alone - it is what the corpse is still
    /// standing on.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class CharacterHitboxes : MonoBehaviour
    {
        private Health _health;
        private Collider[] _hitboxes;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _hitboxes = CollectHitboxes();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
            SetEnabled(true);
        }

        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied(DamageInfo _) => SetEnabled(false);

        /// <summary>Called by RoundDirector when it brings everyone back for a new round.</summary>
        public void ResetForRound() => SetEnabled(true);

        /// <summary>
        /// Found rather than serialized on purpose. A reference you have to drag
        /// in the inspector is a reference someone forgets, and the symptom here
        /// would be "my bullets sometimes do nothing", which is close to
        /// undebuggable from a playtest.
        /// </summary>
        private Collider[] CollectHitboxes()
        {
            int mask = LayerMask.GetMask("Character", "WeakPoint");
            Collider[] all = GetComponentsInChildren<Collider>(true);

            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if ((mask & (1 << all[i].gameObject.layer)) != 0) all[count++] = all[i];
            }

            Collider[] hitboxes = new Collider[count];
            System.Array.Copy(all, hitboxes, count);

            if (count == 0)
            {
                Debug.LogWarning(
                    $"{name} has no colliders on the Character or WeakPoint layers, so it cannot " +
                    "be shot at all. Run Game > Bootstrap Project, then Game > Build Playable Scene.",
                    this);
            }

            return hitboxes;
        }

        private void SetEnabled(bool on)
        {
            if (_hitboxes == null) return;

            foreach (Collider hitbox in _hitboxes)
            {
                if (hitbox != null) hitbox.enabled = on;
            }
        }
    }
}

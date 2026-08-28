using UnityEngine;
using Game.Core;
using Game.Weapons;

namespace Game.Bots
{
    /// <summary>
    /// Lets an enemy shoot, using the same <see cref="Weapon"/> the player does.
    ///
    /// Sharing the weapon code matters: it means an enemy rifle and a player
    /// rifle have identical damage, falloff and fire rate, so the player can
    /// reason about what is shooting at them. It also means one balance change
    /// applies to both sides.
    ///
    /// Difficulty comes from the modifiers here - reaction delay, burst length,
    /// deliberate inaccuracy - never from giving the AI different numbers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotWeaponUser : MonoBehaviour
    {
        [SerializeField] private Weapon weapon;

        [Tooltip("Where the enemy's shots originate. Usually the muzzle.")]
        [SerializeField] private Transform fireOrigin;

        [Header("Reaction")]
        [Tooltip("Seconds between acquiring the target and the first shot. This is the player's window to react and is the single most important difficulty dial.")]
        [SerializeField, Min(0f)] private float reactionDelay = 0.45f;

        [Header("Burst discipline")]
        [Tooltip("Seconds of continuous fire before pausing.")]
        [SerializeField, Min(0.1f)] private float burstSeconds = 0.9f;

        [Tooltip("Seconds of not firing between bursts. Without this the AI is a fire hose and the fight has no rhythm.")]
        [SerializeField, Min(0.1f)] private float betweenBurstSeconds = 1.3f;

        [Header("Accuracy")]
        [Tooltip("Degrees of deliberate error added to the enemy's aim. Zero means it never misses, which is not fun.")]
        [SerializeField, Min(0f)] private float aimErrorDegrees = 2.6f;

        [Tooltip("How fast the aim point catches up with a moving target. Lower makes the enemy easy to outmanoeuvre.")]
        [SerializeField, Min(0.1f)] private float aimTrackingSpeed = 7f;

        [Tooltip("Vertical offset on the target, so enemies aim at the chest rather than the feet.")]
        [SerializeField] private float targetHeightOffset = 1.2f;

        private float _acquiredTime = float.NegativeInfinity;
        private float _burstStartedTime;
        private bool _firing;
        private Vector3 _smoothedAimPoint;
        private Transform _currentTarget;

        private Vector3 Origin =>
            fireOrigin != null ? fireOrigin.position : transform.position + Vector3.up * 1.5f;

        private ShotResolver _resolver;

        private void Awake()
        {
            if (weapon == null)
            {
                Debug.LogError($"{name}: BotWeaponUser has no Weapon assigned.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (weapon != null) weapon.PelletFired += OnPelletFired;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.PelletFired -= OnPelletFired;
        }

        /// <summary>
        /// Bots only ever run on the host, which IS the server, so their shots go
        /// straight to the resolver with no RPC in between. Same resolver, same
        /// damage numbers, same friendly-fire rule as a player's shot.
        /// </summary>
        private void OnPelletFired(Vector3 origin, Vector3 direction)
        {
            if (_resolver == null) _resolver = FindFirstObjectByType<ShotResolver>();
            if (_resolver == null || weapon == null) return;

            _resolver.Resolve(gameObject, weapon.Definition, origin, direction);
        }

        private void Update()
        {
            // The weapon still needs ticking while holding fire so its spread and
            // recoil recover, and so a reload in progress finishes.
            if (_currentTarget == null && weapon != null)
            {
                weapon.Tick(false, true, 0f, Origin, transform.forward);
            }
        }

        public void EngageTarget(Transform target)
        {
            if (weapon == null || target == null) return;

            if (_currentTarget != target)
            {
                _currentTarget = target;
                _acquiredTime = Time.time;
                _smoothedAimPoint = target.position + Vector3.up * targetHeightOffset;
            }

            Vector3 desiredAim = target.position + Vector3.up * targetHeightOffset;

            // Lag the aim point behind the target. This is what lets a player
            // survive by moving, and it is why strafing feels like a skill.
            _smoothedAimPoint = Vector3.Lerp(
                _smoothedAimPoint, desiredAim, 1f - Mathf.Exp(-aimTrackingSpeed * Time.deltaTime));

            bool reacted = Time.time - _acquiredTime >= reactionDelay;
            bool triggerHeld = reacted && UpdateBurstRhythm();

            if (weapon.IsMagazineEmpty && weapon.CanReload)
            {
                // No coroutine: the enemy has no reload animation to sync with, so
                // finishing on the next Update after the delay is indistinguishable.
                if (!weapon.IsReloading) Invoke(nameof(FinishReload), weapon.BeginReload());
                triggerHeld = false;
            }

            Vector3 origin = Origin;
            Vector3 direction = (_smoothedAimPoint - origin).normalized;
            direction = WeaponSpread.Apply(direction, aimErrorDegrees);

            weapon.Tick(triggerHeld, true, 0f, origin, direction);
        }

        public void HoldFire()
        {
            _currentTarget = null;
            _firing = false;
        }

        private bool UpdateBurstRhythm()
        {
            float elapsed = Time.time - _burstStartedTime;

            if (_firing)
            {
                if (elapsed < burstSeconds) return true;
                _firing = false;
                _burstStartedTime = Time.time;
                return false;
            }

            if (elapsed < betweenBurstSeconds) return false;

            _firing = true;
            _burstStartedTime = Time.time;
            return true;
        }

        private void FinishReload() => weapon?.FinishReload();
    }
}

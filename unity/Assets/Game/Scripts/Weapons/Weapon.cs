using System;
using UnityEngine;
using Game.Core;

namespace Game.Weapons
{
    /// <summary>
    /// One equipped weapon: fire control, ammo, reload and hit resolution.
    ///
    /// Hitscan, not projectiles. At the ranges and muzzle velocities in this game
    /// a projectile would be indistinguishable from a raycast while costing an
    /// update per bullet and a pile of pooling code. Add projectiles later for
    /// the weapons that actually need travel time (grenades, launchers).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;

        [Tooltip("Where the trace starts and the muzzle flash spawns.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Everything a bullet can hit. Exclude the shooter's own layer or every shot hits the barrel of the gun.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Tooltip("Colliders on this layer count as weak points and deal critical damage.")]
        [SerializeField] private LayerMask weakPointMask;

        [SerializeField] private AudioSource audioSource;

        private float _nextShotTime;
        private int _burstRemaining;
        private bool _triggerHeldLastFrame;

        private WeaponSpread _spread;
        private WeaponRecoil _recoil;

        public WeaponDefinition Definition => definition;
        public int MagazineAmmo { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsMagazineEmpty => MagazineAmmo <= 0;
        public bool CanReload => !IsReloading && ReserveAmmo > 0 && MagazineAmmo < definition.MagazineSize;

        /// <summary>Camera pitch/yaw offset the aim controller should apply this frame.</summary>
        public Vector2 RecoilOffset => _recoil.Current;

        /// <summary>Raised once per trigger event that actually spent a round.</summary>
        public event Action Fired;

        /// <summary>Raised per pellet that hit something damageable.</summary>
        public event Action<DamageInfo> HitDamageable;

        /// <summary>Raised when a hit killed the target.</summary>
        public event Action<GameObject> Killed;

        public event Action AmmoChanged;

        /// <summary>Raised with the noise radius so the AI hearing system can react.</summary>
        public event Action<Vector3, float> NoiseEmitted;

        private void Awake()
        {
            if (definition == null)
            {
                Debug.LogError($"{name}: Weapon has no WeaponDefinition assigned.", this);
                enabled = false;
                return;
            }

            MagazineAmmo = definition.MagazineSize;
            ReserveAmmo = definition.StartingReserve;
        }

        private void Update()
        {
            if (definition == null) return;
            _spread.Recover(definition, Time.deltaTime);
            _recoil.Recover(definition, Time.deltaTime);
        }

        /// <summary>
        /// Drive this every frame from the owner. Passing the trigger state rather
        /// than exposing Fire()/StopFire() keeps burst and semi-auto edge detection
        /// in one place instead of spread across the player and the AI.
        /// </summary>
        /// <param name="aiming">Tightens the cone.</param>
        /// <param name="moveSpeed">Owner's planar speed, which widens the cone.</param>
        public void Tick(bool triggerHeld, bool aiming, float moveSpeed, Vector3 aimOrigin, Vector3 aimDirection)
        {
            if (definition == null || IsReloading)
            {
                _triggerHeldLastFrame = triggerHeld;
                return;
            }

            bool justPressed = triggerHeld && !_triggerHeldLastFrame;
            _triggerHeldLastFrame = triggerHeld;

            if (!triggerHeld) _spread.Reset();

            bool wantsToFire = definition.Mode switch
            {
                FireMode.FullAuto => triggerHeld,
                FireMode.SemiAuto => justPressed,
                FireMode.Burst => justPressed || _burstRemaining > 0,
                _ => false
            };

            if (!wantsToFire || Time.time < _nextShotTime) return;

            if (IsMagazineEmpty)
            {
                if (justPressed) PlayClip(definition.EmptyClip);
                _burstRemaining = 0;
                return;
            }

            if (definition.Mode == FireMode.Burst && justPressed)
            {
                _burstRemaining = definition.BurstCount;
            }

            FireOnce(aiming, moveSpeed, aimOrigin, aimDirection);

            if (definition.Mode == FireMode.Burst && _burstRemaining > 0) _burstRemaining--;
        }

        private void FireOnce(bool aiming, float moveSpeed, Vector3 aimOrigin, Vector3 aimDirection)
        {
            _nextShotTime = Time.time + definition.SecondsBetweenShots;

            MagazineAmmo--;
            AmmoChanged?.Invoke();

            float cone = _spread.CurrentDegrees(definition, aiming, moveSpeed);

            for (int i = 0; i < definition.PelletsPerShot; i++)
            {
                TracePellet(aimOrigin, WeaponSpread.Apply(aimDirection, cone));
            }

            _spread.RegisterShot(definition);
            _recoil.RegisterShot(definition);

            SpawnMuzzleFlash();
            PlayClip(definition.FireClip);

            Fired?.Invoke();
            NoiseEmitted?.Invoke(transform.position, definition.NoiseRadius);
        }

        private void TracePellet(Vector3 origin, Vector3 direction)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit,
                    definition.Range, hitMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            bool critical = (weakPointMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            float damage = definition.DamageAtDistance(hit.distance);

            var info = new DamageInfo(damage, hit.point, direction, gameObject, critical);

            IDamageable target = DamageResolver.Resolve(hit.collider);
            if (target == null || !target.IsAlive) return;

            target.ApplyDamage(in info);
            HitDamageable?.Invoke(info);

            if (!target.IsAlive) Killed?.Invoke(hit.collider.gameObject);
        }

        /// <summary>
        /// Starts a reload. The owner is responsible for the timer; call
        /// <see cref="FinishReload"/> when the animation event or coroutine fires.
        /// </summary>
        public float BeginReload()
        {
            if (!CanReload) return 0f;

            IsReloading = true;
            PlayClip(definition.ReloadClip);

            // A round still in the chamber means the bolt does not have to be
            // cycled, so keeping one loaded is genuinely faster. Players who
            // notice this feel clever, which is the whole point.
            return MagazineAmmo > 0 ? definition.TacticalReloadSeconds : definition.ReloadSeconds;
        }

        public void FinishReload()
        {
            if (!IsReloading) return;

            int wanted = definition.MagazineSize - MagazineAmmo;
            int moved = Mathf.Min(wanted, ReserveAmmo);

            MagazineAmmo += moved;
            ReserveAmmo -= moved;

            IsReloading = false;
            _spread.Reset();
            AmmoChanged?.Invoke();
        }

        public void CancelReload() => IsReloading = false;

        public void AddReserveAmmo(int rounds)
        {
            if (rounds <= 0) return;
            ReserveAmmo += rounds;
            AmmoChanged?.Invoke();
        }

        private void SpawnMuzzleFlash()
        {
            if (definition.MuzzleFlashPrefab == null || muzzle == null) return;
            GameObject flash = Instantiate(definition.MuzzleFlashPrefab, muzzle.position, muzzle.rotation, muzzle);
            Destroy(flash, 1.5f);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip);
        }
    }
}

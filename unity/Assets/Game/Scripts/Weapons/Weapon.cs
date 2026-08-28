using System;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// One equipped weapon: fire control, ammo, reload and spread.
    ///
    /// It deliberately does NOT resolve hits. It decides that a shot happened and
    /// where each pellet went, then raises <see cref="PelletFired"/>. Something
    /// else traces it.
    ///
    /// That seam exists because this is a multiplayer game. If the weapon applied
    /// damage locally, a kill would only exist on the shooter's machine and
    /// everyone else would watch an unharmed player keep running. The owning
    /// client predicts the fire rate, ammo and recoil - those must feel instant -
    /// and ships the ray to the server, which is the only thing that decides
    /// whether anyone was hit.
    ///
    /// Hitscan, not projectiles. At these ranges a projectile would be
    /// indistinguishable from a raycast while costing an update per bullet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;

        [Tooltip("Where the trace starts and the muzzle flash spawns.")]
        [SerializeField] private Transform muzzle;

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

        /// <summary>
        /// Raised once per pellet with (origin, direction). Whoever subscribes is
        /// responsible for tracing it - locally for a bot on the server, or over
        /// an RPC for a player. Nothing is damaged until someone does.
        /// </summary>
        public event Action<Vector3, Vector3> PelletFired;

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
                PelletFired?.Invoke(aimOrigin, WeaponSpread.Apply(aimDirection, cone));
            }

            _spread.RegisterShot(definition);
            _recoil.RegisterShot(definition);

            SpawnMuzzleFlash();
            PlayClip(definition.FireClip);

            Fired?.Invoke();
            NoiseEmitted?.Invoke(transform.position, definition.NoiseRadius);
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

        /// <summary>
        /// Full magazine, full reserve, nothing in progress.
        ///
        /// Called at the start of every round. Nothing else refills ammo - there
        /// is no buy phase and there are no pickups - so without this you carry
        /// whatever you had left into every remaining round and the match ends
        /// with everyone dry.
        /// </summary>
        public void ResetForRound()
        {
            CancelReload();

            MagazineAmmo = definition != null ? definition.MagazineSize : 0;
            ReserveAmmo = definition != null ? definition.StartingReserve : 0;

            _burstRemaining = 0;
            _nextShotTime = 0f;
            _triggerHeldLastFrame = false;
            _spread.Reset();
            _recoil.Reset();

            AmmoChanged?.Invoke();
        }

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

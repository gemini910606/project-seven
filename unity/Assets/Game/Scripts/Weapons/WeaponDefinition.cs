using UnityEngine;

namespace Game.Weapons
{
    public enum FireMode
    {
        SemiAuto,
        FullAuto,
        Burst
    }

    /// <summary>
    /// All the tuning for one weapon, as data rather than code.
    ///
    /// The point of putting this in a ScriptableObject is that balancing stops
    /// being a programming task: someone can open the asset, change the damage
    /// curve, and hit play. Every number here is per-weapon; nothing about a
    /// specific gun belongs in <see cref="Weapon"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Weapon Definition", fileName = "Weapon_")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Rifle";

        [Tooltip("Prefab of the weapon model, parented to the character's hand socket on equip.")]
        public GameObject ViewModelPrefab;

        [Header("Firing")]
        public FireMode Mode = FireMode.FullAuto;

        [Tooltip("Rounds per minute. 600 rpm = one shot every 100ms.")]
        [Min(1f)] public float RoundsPerMinute = 620f;

        [Tooltip("Shots per trigger pull in Burst mode.")]
        [Min(1)] public int BurstCount = 3;

        [Tooltip("Pellets per shot. Above 1 makes it a shotgun.")]
        [Min(1)] public int PelletsPerShot = 1;

        [Header("Damage")]
        [Min(0f)] public float DamagePerPellet = 24f;

        [Tooltip("Damage multiplier over distance. X axis is metres, Y is a multiplier. Keeps a rifle from being a sniper at 300m.")]
        public AnimationCurve DamageFalloff = AnimationCurve.Linear(0f, 1f, 120f, 0.45f);

        [Tooltip("Maximum trace distance in metres. Beyond this the shot simply misses.")]
        [Min(1f)] public float Range = 180f;

        [Header("Accuracy")]
        [Tooltip("Cone half-angle in degrees while standing still and aiming.")]
        [Min(0f)] public float BaseSpreadDegrees = 0.35f;

        [Tooltip("Added to the cone while hip-firing.")]
        [Min(0f)] public float HipFireSpreadDegrees = 3.2f;

        [Tooltip("Added to the cone per m/s of movement.")]
        [Min(0f)] public float SpreadPerMoveSpeed = 0.28f;

        [Tooltip("Added to the cone per consecutive shot, cleared when the trigger is released.")]
        [Min(0f)] public float SpreadPerShot = 0.22f;

        [Tooltip("Ceiling on accumulated spread, so a held trigger stays usable.")]
        [Min(0f)] public float MaxSpreadDegrees = 6f;

        [Tooltip("Degrees of accumulated spread recovered per second.")]
        [Min(0f)] public float SpreadRecoveryPerSecond = 7f;

        [Header("Recoil")]
        [Tooltip("Degrees the camera kicks up per shot.")]
        [Min(0f)] public float RecoilVertical = 0.55f;

        [Tooltip("Maximum absolute horizontal kick per shot, in degrees. Sign alternates.")]
        [Min(0f)] public float RecoilHorizontal = 0.22f;

        [Tooltip("How fast the camera returns to where it was aiming.")]
        [Min(0f)] public float RecoilRecoveryPerSecond = 5.5f;

        [Header("Ammo")]
        [Min(1)] public int MagazineSize = 30;
        [Min(0)] public int StartingReserve = 120;
        [Min(0.1f)] public float ReloadSeconds = 2.1f;

        [Tooltip("Reloading with a round still chambered is faster. Set equal to ReloadSeconds to disable.")]
        [Min(0.1f)] public float TacticalReloadSeconds = 1.7f;

        [Header("Audio and noise")]
        public AudioClip FireClip;
        public AudioClip ReloadClip;
        public AudioClip EmptyClip;

        [Tooltip("Metres within which AI hears this weapon. Suppressed weapons should be small.")]
        [Min(0f)] public float NoiseRadius = 45f;

        [Header("Presentation")]
        public GameObject MuzzleFlashPrefab;

        [Tooltip("Field of view multiplier while aiming down sights.")]
        [Range(0.3f, 1f)] public float AimFovScale = 0.72f;

        /// <summary>Seconds between shots, derived from RPM.</summary>
        public float SecondsBetweenShots => 60f / Mathf.Max(1f, RoundsPerMinute);

        /// <summary>Damage a single pellet does at a given distance.</summary>
        public float DamageAtDistance(float metres) =>
            DamagePerPellet * Mathf.Max(0f, DamageFalloff.Evaluate(metres));

        private void OnValidate()
        {
            if (TacticalReloadSeconds > ReloadSeconds) TacticalReloadSeconds = ReloadSeconds;
            if (MaxSpreadDegrees < BaseSpreadDegrees) MaxSpreadDegrees = BaseSpreadDegrees;
        }
    }
}

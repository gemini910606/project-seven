using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Hit points, regeneration and death for the player and every AI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Header("Pool")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Header("Regeneration")]
        [Tooltip("Health per second once regeneration starts. Zero disables it.")]
        [SerializeField, Min(0f)] private float regenPerSecond = 12f;

        [Tooltip("Seconds without taking damage before regeneration starts.")]
        [SerializeField, Min(0f)] private float regenDelay = 5f;

        [Tooltip("Regeneration stops here, so a hurt player stays punished until they heal properly.")]
        [SerializeField, Range(0f, 1f)] private float regenCeiling = 0.7f;

        [Header("Damage shaping")]
        [Tooltip("Multiplier applied to hits flagged as critical (headshots).")]
        [SerializeField, Min(1f)] private float criticalMultiplier = 2.5f;

        [Tooltip("Minimum seconds between two damage events. Stops a shotgun's pellets from being eight separate hit reactions.")]
        [SerializeField, Min(0f)] private float damageCooldown = 0.03f;

        private float _current;
        private float _lastDamageTime = float.NegativeInfinity;

        public float Current => _current;
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? _current / maxHealth : 0f;
        public bool IsAlive => _current > 0f;

        /// <summary>Fired for every applied hit, with the damage actually dealt.</summary>
        public event Action<DamageInfo, float> Damaged;

        /// <summary>Fired exactly once, on the transition to zero.</summary>
        public event Action<DamageInfo> Died;

        private void Awake() => _current = maxHealth;

        private void Update()
        {
            if (!IsAlive || regenPerSecond <= 0f) return;
            if (Time.time - _lastDamageTime < regenDelay) return;

            float ceiling = maxHealth * regenCeiling;
            if (_current >= ceiling) return;

            _current = Mathf.Min(ceiling, _current + regenPerSecond * Time.deltaTime);
        }

        public float ApplyDamage(in DamageInfo info)
        {
            if (!IsAlive) return 0f;
            if (Time.time - _lastDamageTime < damageCooldown) return 0f;

            float damage = info.Amount * (info.IsCritical ? criticalMultiplier : 1f);
            damage = Mathf.Min(damage, _current);

            _current -= damage;
            _lastDamageTime = Time.time;

            Damaged?.Invoke(info, damage);

            if (_current <= 0f)
            {
                _current = 0f;
                Died?.Invoke(info);
            }

            return damage;
        }

        /// <summary>Restores health without going over the maximum. Ignored when dead.</summary>
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _current = Mathf.Min(maxHealth, _current + amount);
        }

        /// <summary>Used by the spawner when recycling a pooled enemy.</summary>
        public void ResetToFull()
        {
            _current = maxHealth;
            _lastDamageTime = float.NegativeInfinity;
        }
    }
}

using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Hit points and death, for the player and every bot.
    ///
    /// There is no regeneration and no healing, on purpose. This is a round-based
    /// game with one life a round: damage you take is meant to follow you until
    /// the round ends, which is what makes chipping someone for 70 and forcing
    /// them off the site worth anything. <see cref="ResetToFull"/> at the start of
    /// each round is the only way health comes back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Header("Pool")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Header("Damage shaping")]
        [Tooltip("Multiplier applied to hits flagged as critical (headshots).")]
        [SerializeField, Min(1f)] private float criticalMultiplier = 2.5f;

        private float _current;

        public float Current => _current;
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? _current / maxHealth : 0f;
        public bool IsAlive => _current > 0f;

        /// <summary>Fired for every applied hit, with the damage actually dealt.</summary>
        public event Action<DamageInfo, float> Damaged;

        /// <summary>Fired exactly once, on the transition to zero.</summary>
        public event Action<DamageInfo> Died;

        private void Awake() => _current = maxHealth;

        public float ApplyDamage(in DamageInfo info)
        {
            if (!IsAlive) return 0f;

            float damage = info.Amount * (info.IsCritical ? criticalMultiplier : 1f);
            damage = Mathf.Min(damage, _current);

            _current -= damage;

            Damaged?.Invoke(info, damage);

            if (_current <= 0f)
            {
                _current = 0f;
                Died?.Invoke(info);
            }

            return damage;
        }

        /// <summary>Called by RoundDirector when it respawns everyone for a new round.</summary>
        public void ResetToFull() => _current = maxHealth;
    }
}

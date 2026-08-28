using System;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// The heat meter: this project's answer to a wanted level.
    ///
    /// Heat is a continuous 0..1-per-level value rather than an integer, because
    /// the interesting design space is in the decay - the moment where you are
    /// hiding in a stairwell watching the meter tick down and deciding whether
    /// you can afford one more objective. Integers alone cannot express that.
    ///
    /// One instance lives on the mission director. Everything that raises heat
    /// (a gunshot heard, a body found, a camera spotting you) calls
    /// <see cref="AddHeat"/>; the level is derived.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlertSystem : MonoBehaviour
    {
        public const int MaxLevel = 5;

        [Header("Escalation")]
        [Tooltip("Heat needed to reach each level, index 0 = level 1. Must be ascending.")]
        [SerializeField]
        private float[] levelThresholds = { 20f, 55f, 110f, 200f, 320f };

        [Tooltip("Heat is clamped here so a long firefight cannot bank infinite escalation.")]
        [SerializeField, Min(1f)] private float maxHeat = 400f;

        [Header("Decay")]
        [Tooltip("Seconds of not being seen or heard before heat starts falling.")]
        [SerializeField, Min(0f)] private float cooldownDelay = 12f;

        [Tooltip("Heat lost per second while cooling down.")]
        [SerializeField, Min(0f)] private float decayPerSecond = 9f;

        [Tooltip("Heat never falls below the floor of the current level while any enemy can see the player. Prevents a level flickering during a fight.")]
        [SerializeField] private bool holdLevelWhileSeen = true;

        [Header("Heat values")]
        [SerializeField, Min(0f)] private float heatPerGunshotHeard = 6f;
        [SerializeField, Min(0f)] private float heatPerSpotted = 25f;
        [SerializeField, Min(0f)] private float heatPerBodyFound = 18f;
        [SerializeField, Min(0f)] private float heatPerKill = 12f;

        private float _heat;
        private float _lastHeatTime = float.NegativeInfinity;
        private int _level;
        private int _visibleWitnesses;

        /// <summary>Current integer level, 0..5.</summary>
        public int Level => _level;

        /// <summary>Raw heat, for debug UI.</summary>
        public float Heat => _heat;

        /// <summary>
        /// Progress towards the next level, 0..1. Feeds the HUD meter that fills
        /// between stars. Returns 1 at max level.
        /// </summary>
        public float ProgressToNextLevel
        {
            get
            {
                if (_level >= MaxLevel) return 1f;
                float floor = _level == 0 ? 0f : levelThresholds[_level - 1];
                float ceiling = levelThresholds[_level];
                return ceiling > floor ? Mathf.Clamp01((_heat - floor) / (ceiling - floor)) : 0f;
            }
        }

        public bool IsCoolingDown => Time.time - _lastHeatTime >= cooldownDelay;

        /// <summary>Fired when the integer level changes, with (previous, current).</summary>
        public event Action<int, int> LevelChanged;

        private void Update()
        {
            if (_heat > 0f && IsCoolingDown)
            {
                float floor = FloorForHoldingLevel();
                _heat = Mathf.Max(floor, _heat - decayPerSecond * Time.deltaTime);
                RecomputeLevel();
            }
        }

        public void AddHeat(float amount)
        {
            if (amount <= 0f) return;
            _heat = Mathf.Min(maxHeat, _heat + amount);
            _lastHeatTime = Time.time;
            RecomputeLevel();
        }

        public void ReportGunshotHeard() => AddHeat(heatPerGunshotHeard);

        public void ReportPlayerSpotted() => AddHeat(heatPerSpotted);

        public void ReportBodyFound() => AddHeat(heatPerBodyFound);

        public void ReportKill() => AddHeat(heatPerKill);

        /// <summary>
        /// Called by perception when an enemy gains or loses sight of the player.
        /// The count, not a bool, because several enemies can see you at once and
        /// the last one losing sight is what should start the cooldown.
        /// </summary>
        public void SetWitnessVisible(bool visible)
        {
            _visibleWitnesses = Mathf.Max(0, _visibleWitnesses + (visible ? 1 : -1));
            if (visible) _lastHeatTime = Time.time;
        }

        /// <summary>Instantly clears all heat. For mission scripting and testing.</summary>
        public void ClearHeat()
        {
            _heat = 0f;
            _visibleWitnesses = 0;
            RecomputeLevel();
        }

        private float FloorForHoldingLevel()
        {
            if (!holdLevelWhileSeen || _visibleWitnesses == 0 || _level == 0) return 0f;
            return levelThresholds[_level - 1];
        }

        private void RecomputeLevel()
        {
            int computed = 0;
            for (int i = 0; i < levelThresholds.Length && i < MaxLevel; i++)
            {
                if (_heat >= levelThresholds[i]) computed = i + 1;
            }

            if (computed == _level) return;

            int previous = _level;
            _level = computed;
            LevelChanged?.Invoke(previous, computed);
        }

        private void OnValidate()
        {
            // An unsorted threshold array silently breaks RecomputeLevel, and the
            // symptom (levels that skip or stick) is miserable to debug in play mode.
            for (int i = 1; i < levelThresholds.Length; i++)
            {
                if (levelThresholds[i] <= levelThresholds[i - 1])
                {
                    Debug.LogError(
                        $"{nameof(AlertSystem)}: levelThresholds must ascend. " +
                        $"Index {i} ({levelThresholds[i]}) is not greater than index {i - 1} ({levelThresholds[i - 1]}).",
                        this);
                    break;
                }
            }
        }
    }
}

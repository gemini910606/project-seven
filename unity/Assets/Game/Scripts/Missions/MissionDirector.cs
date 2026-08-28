using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.AI;
using Game.Backend;
using Game.Core;
using Game.Player;
using Game.Weapons;

namespace Game.Missions
{
    /// <summary>
    /// Runs one mission from spawn to extraction or death.
    ///
    /// This is the only object that knows what a "run" is. It owns the stats,
    /// decides when the run ends, computes the score and hands it to the backend.
    /// Everything else raises events and stays ignorant of the run's existence,
    /// which is what lets the weapon code be reused by AI and the alert system be
    /// tested on its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionDirector : MonoBehaviour
    {
        [Header("Mission")]
        [SerializeField] private MissionDefinition mission;

        [Header("Scene references")]
        [SerializeField] private PlayerController player;
        [SerializeField] private AlertSystem alertSystem;
        [SerializeField] private BackendClient backend;

        [Tooltip("Zones in the scene. Found automatically if left empty.")]
        [SerializeField] private List<ObjectiveZone> zones = new();

        private readonly List<ObjectiveProgress> _progress = new();
        private readonly HashSet<string> _occupiedZones = new();

        private RunStats _stats;
        private float _runStartTime;
        private bool _runActive;
        private Coroutine _extractionRoutine;

        public MissionDefinition Mission => mission;
        public IReadOnlyList<ObjectiveProgress> Objectives => _progress;
        public RunStats Stats => _stats;
        public bool IsRunActive => _runActive;

        /// <summary>Seconds spent in the current run.</summary>
        public float Elapsed => _runActive ? Time.time - _runStartTime : _stats?.DurationSeconds ?? 0f;

        /// <summary>Raised when an objective completes, for HUD feedback.</summary>
        public event Action<ObjectiveProgress> ObjectiveCompleted;

        /// <summary>Raised when the run ends, with the final stats and score.</summary>
        public event Action<RunStats, int> RunEnded;

        /// <summary>Raised while extracting, with remaining hold seconds.</summary>
        public event Action<float> ExtractionProgress;

        private void Awake()
        {
            if (mission == null)
            {
                Debug.LogError($"{name}: MissionDirector has no MissionDefinition.", this);
                enabled = false;
                return;
            }

            if (zones.Count == 0) zones.AddRange(FindObjectsByType<ObjectiveZone>(FindObjectsSortMode.None));
        }

        private void OnEnable()
        {
            foreach (ObjectiveZone zone in zones)
            {
                if (zone != null) zone.OccupancyChanged += OnZoneOccupancyChanged;
            }
            CollectibleItem.Collected += OnItemCollected;
        }

        private void OnDisable()
        {
            foreach (ObjectiveZone zone in zones)
            {
                if (zone != null) zone.OccupancyChanged -= OnZoneOccupancyChanged;
            }
            CollectibleItem.Collected -= OnItemCollected;

            UnhookPlayer();
        }

        private void Start() => BeginRun();

        private void Update()
        {
            if (!_runActive) return;

            _stats.RecordAlert(alertSystem != null ? alertSystem.Level : 0);

            if (mission.TimeLimitSeconds > 0f && Elapsed >= mission.TimeLimitSeconds)
            {
                EndRun(RunOutcome.Died);
                return;
            }

            if (mission.FailAtAlertLevel > 0
                && alertSystem != null
                && alertSystem.Level >= mission.FailAtAlertLevel)
            {
                EndRun(RunOutcome.Died);
            }
        }

        public void BeginRun()
        {
            _stats = new RunStats { MissionId = mission.MissionId, ObjectivesTotal = mission.RequiredObjectiveCount };
            _progress.Clear();
            _occupiedZones.Clear();

            foreach (MissionObjective objective in mission.Objectives)
            {
                if (objective == null) continue;

                ObjectiveProgress progress = objective.CreateProgress();
                progress.Completed += OnObjectiveCompleted;
                _progress.Add(progress);
            }

            alertSystem?.ClearHeat();
            HookPlayer();

            _runStartTime = Time.time;
            _runActive = true;
        }

        /// <summary>Ends the run without an outcome, e.g. the player quit to menu.</summary>
        public void AbortRun()
        {
            if (_runActive) EndRun(RunOutcome.Aborted);
        }

        // ------------------------------------------------------------------
        // Objective tracking
        // ------------------------------------------------------------------

        private void OnZoneOccupancyChanged(string zoneId, bool inside)
        {
            if (!_runActive) return;

            if (inside) _occupiedZones.Add(zoneId);
            else _occupiedZones.Remove(zoneId);

            ObjectiveProgress active = FirstIncomplete();
            if (active == null) return;

            switch (active.Definition)
            {
                case ReachZoneObjective reach when inside && reach.ZoneId == zoneId:
                    active.Advance();
                    break;

                case ExtractObjective extract when extract.ZoneId == zoneId:
                    if (inside) StartExtraction(active, extract);
                    else CancelExtraction();
                    break;
            }
        }

        private void OnItemCollected(string itemId)
        {
            if (!_runActive) return;

            // Any incomplete collect objective for this item counts, not just the
            // first in the list - otherwise picking things up out of order stalls.
            foreach (ObjectiveProgress progress in _progress)
            {
                if (progress.IsComplete) continue;
                if (progress.Definition is CollectObjective collect && collect.ItemId == itemId)
                {
                    progress.Advance();
                    return;
                }
            }
        }

        private void StartExtraction(ObjectiveProgress progress, ExtractObjective extract)
        {
            // Extraction only opens once everything else is done, so the zone is
            // not a shortcut past the mission.
            if (HasIncompleteRequiredBefore(progress)) return;

            CancelExtraction();
            _extractionRoutine = StartCoroutine(ExtractionCountdown(progress, extract));
        }

        private void CancelExtraction()
        {
            if (_extractionRoutine == null) return;

            StopCoroutine(_extractionRoutine);
            _extractionRoutine = null;
            ExtractionProgress?.Invoke(0f);
        }

        private IEnumerator ExtractionCountdown(ObjectiveProgress progress, ExtractObjective extract)
        {
            float remaining = extract.HoldSeconds;

            while (remaining > 0f)
            {
                if (!_occupiedZones.Contains(extract.ZoneId))
                {
                    ExtractionProgress?.Invoke(0f);
                    _extractionRoutine = null;
                    yield break;
                }

                ExtractionProgress?.Invoke(remaining);
                remaining -= Time.deltaTime;
                yield return null;
            }

            ExtractionProgress?.Invoke(0f);
            _extractionRoutine = null;
            progress.ForceComplete();
        }

        private void OnObjectiveCompleted(ObjectiveProgress progress)
        {
            if (!progress.Definition.Optional) _stats.ObjectivesCompleted++;

            ObjectiveCompleted?.Invoke(progress);

            if (progress.Definition is ExtractObjective) EndRun(RunOutcome.Extracted);
        }

        private ObjectiveProgress FirstIncomplete()
        {
            foreach (ObjectiveProgress progress in _progress)
            {
                if (!progress.IsComplete) return progress;
            }
            return null;
        }

        private bool HasIncompleteRequiredBefore(ObjectiveProgress target)
        {
            foreach (ObjectiveProgress progress in _progress)
            {
                if (progress == target) return false;
                if (!progress.IsComplete && !progress.Definition.Optional) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Player hooks
        // ------------------------------------------------------------------

        private void HookPlayer()
        {
            if (player == null) return;

            player.Health.Died += OnPlayerDied;
            player.Health.Damaged += OnPlayerDamaged;

            Weapon weapon = player.Weapons != null ? player.Weapons.Current : null;
            if (weapon == null) return;

            weapon.Fired += OnShotFired;
            weapon.HitDamageable += OnShotHit;
            weapon.Killed += OnEnemyKilled;
            weapon.NoiseEmitted += OnWeaponNoise;
        }

        private void UnhookPlayer()
        {
            if (player == null) return;

            player.Health.Died -= OnPlayerDied;
            player.Health.Damaged -= OnPlayerDamaged;

            Weapon weapon = player.Weapons != null ? player.Weapons.Current : null;
            if (weapon == null) return;

            weapon.Fired -= OnShotFired;
            weapon.HitDamageable -= OnShotHit;
            weapon.Killed -= OnEnemyKilled;
            weapon.NoiseEmitted -= OnWeaponNoise;
        }

        private void OnShotFired() => _stats.RecordShot();

        private void OnShotHit(DamageInfo info) => _stats.RecordHit();

        private void OnEnemyKilled(GameObject victim)
        {
            _stats.RecordKill();

            foreach (ObjectiveProgress progress in _progress)
            {
                if (progress.IsComplete) continue;
                if (progress.Definition is EliminateObjective eliminate
                    && (string.IsNullOrEmpty(eliminate.EnemyTag) || victim.CompareTag(eliminate.EnemyTag)))
                {
                    progress.Advance();
                    break;
                }
            }
        }

        private void OnWeaponNoise(Vector3 position, float radius)
        {
            NoiseSystem.EmitSafe(position, radius);
            alertSystem?.ReportGunshotHeard();
        }

        private void OnPlayerDamaged(DamageInfo info, float applied) => _stats.RecordDamageTaken(applied);

        private void OnPlayerDied(DamageInfo info) => EndRun(RunOutcome.Died);

        // ------------------------------------------------------------------
        // Ending
        // ------------------------------------------------------------------

        private void EndRun(RunOutcome outcome)
        {
            if (!_runActive) return;

            CancelExtraction();

            _runActive = false;
            _stats.DurationSeconds = Time.time - _runStartTime;
            _stats.Outcome = outcome;
            _stats.RecordAlert(alertSystem != null ? alertSystem.Level : 0);

            int score = ScoreCalculator.Compute(_stats);

            UnhookPlayer();
            RunEnded?.Invoke(_stats, score);

            // Submission is fire-and-forget. A failed upload must never block the
            // results screen; the player has finished playing and does not care.
            if (backend != null && backend.IsEnabled && outcome != RunOutcome.Aborted)
            {
                StartCoroutine(backend.SubmitRun(_stats, score));
            }
        }
    }
}

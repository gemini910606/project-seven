using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.AI
{
    /// <summary>
    /// Spawns reinforcements as the heat rises.
    ///
    /// This is the system that turns the alert level from a number into pressure.
    /// Level 1 sends a pair of guards to look; level 4 sends a squad that already
    /// knows where you are. It is also the main lever on difficulty, and the main
    /// risk to frame rate, so both the concurrent cap and the interval are
    /// per-level rather than global.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnDirector : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Wave
        {
            [Tooltip("Alert level at which this wave becomes active.")]
            [Range(0, AlertSystem.MaxLevel)] public int AlertLevel = 1;

            public GameObject EnemyPrefab;

            [Tooltip("Seconds between spawns while this wave is active.")]
            [Min(1f)] public float Interval = 12f;

            [Tooltip("Maximum enemies alive from this director. The hard limit on how bad the frame rate can get.")]
            [Min(1)] public int ConcurrentCap = 8;

            [Tooltip("Spawn only outside this distance from the player, so nothing appears in front of them.")]
            [Min(0f)] public float MinDistanceFromPlayer = 35f;
        }

        [SerializeField] private AlertSystem alertSystem;
        [SerializeField] private Transform player;

        [Tooltip("Points reinforcements arrive from. Place them at street entrances, not in the open.")]
        [SerializeField] private List<Transform> spawnPoints = new();

        [Tooltip("Ordered by AlertLevel. The highest wave whose level is met is used.")]
        [SerializeField] private List<Wave> waves = new();

        private readonly List<GameObject> _alive = new();
        private float _nextSpawnTime;

        public int AliveCount
        {
            get
            {
                _alive.RemoveAll(enemy => enemy == null);
                return _alive.Count;
            }
        }

        private void Update()
        {
            if (alertSystem == null || player == null || spawnPoints.Count == 0) return;

            Wave wave = ActiveWave();
            if (wave == null || wave.EnemyPrefab == null) return;

            if (AliveCount >= wave.ConcurrentCap) return;
            if (Time.time < _nextSpawnTime) return;

            Transform point = PickSpawnPoint(wave.MinDistanceFromPlayer);
            if (point == null) return;

            Spawn(wave.EnemyPrefab, point);
            _nextSpawnTime = Time.time + wave.Interval;
        }

        private Wave ActiveWave()
        {
            Wave best = null;
            foreach (Wave wave in waves)
            {
                if (wave.AlertLevel > alertSystem.Level) continue;
                if (best == null || wave.AlertLevel > best.AlertLevel) best = wave;
            }
            return best;
        }

        private Transform PickSpawnPoint(float minDistance)
        {
            // One shuffled pass rather than repeated random picks, so a scene
            // where every point is too close terminates instead of spinning.
            int count = spawnPoints.Count;
            int start = Random.Range(0, count);

            for (int i = 0; i < count; i++)
            {
                Transform candidate = spawnPoints[(start + i) % count];
                if (candidate == null) continue;
                if (Vector3.Distance(candidate.position, player.position) >= minDistance) return candidate;
            }

            return null;
        }

        private void Spawn(GameObject prefab, Transform point)
        {
            GameObject enemy = Instantiate(prefab, point.position, point.rotation);
            _alive.Add(enemy);

            if (enemy.TryGetComponent(out EnemyBrain brain)) brain.Initialise(player, alertSystem);

            // At high heat the reinforcements are responding to a known location,
            // not stumbling across it. Making them arrive already alerted is what
            // stops level 4 feeling identical to level 1.
            if (alertSystem.Level >= 3
                && enemy.TryGetComponent(out EnemyPerception perception))
            {
                perception.ForceAlert(player.position);
            }

            if (enemy.TryGetComponent(out Health health))
            {
                health.Died += _ => _alive.Remove(enemy);
            }
        }
    }
}

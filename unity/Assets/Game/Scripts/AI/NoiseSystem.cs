using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Routes noise events to everything that can hear.
    ///
    /// A registry rather than Physics.OverlapSphere per gunshot: an enemy count
    /// in the dozens is far smaller than the collider count in a city block, so
    /// iterating listeners is both cheaper and completely predictable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseSystem : MonoBehaviour
    {
        private static NoiseSystem _instance;

        private readonly List<EnemyPerception> _listeners = new();

        public static NoiseSystem Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"{name}: a second NoiseSystem was destroyed. There should be exactly one.", this);
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void Register(EnemyPerception listener)
        {
            if (listener != null && !_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void Unregister(EnemyPerception listener) => _listeners.Remove(listener);

        /// <summary>Emits a noise at a world position with a radius in metres.</summary>
        public void Emit(Vector3 position, float radius)
        {
            // Iterate backwards so a listener destroyed during the loop (an enemy
            // killed by the same shot that made the noise) does not shift indices.
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                EnemyPerception listener = _listeners[i];
                if (listener == null)
                {
                    _listeners.RemoveAt(i);
                    continue;
                }
                listener.HearNoise(position, radius);
            }
        }

        /// <summary>Convenience for callers that may run before the system exists.</summary>
        public static void EmitSafe(Vector3 position, float radius) =>
            _instance?.Emit(position, radius);
    }
}

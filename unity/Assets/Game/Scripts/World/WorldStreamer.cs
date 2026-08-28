using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.World
{
    /// <summary>
    /// Loads and unloads scene chunks around the player.
    ///
    /// Additive scenes rather than Addressables for the world itself. Both work,
    /// but scenes let you author a district by opening it and moving things, and
    /// they keep lighting data per chunk. Use Addressables for content you want
    /// to patch remotely (see docs/CLOUDFLARE.md); use scenes for the map.
    ///
    /// The hysteresis between load and unload radius is not optional. Without it,
    /// a player standing exactly on a chunk boundary loads and unloads the same
    /// scene every few frames, which is a hitch you will chase for days.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldStreamer : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Chunk
        {
            [Tooltip("Scene name exactly as it appears in Build Settings.")]
            public string SceneName;

            [Tooltip("Centre of the chunk in world space.")]
            public Vector3 Centre;

            [Tooltip("Half-extent used for the distance test, in metres.")]
            public float Radius = 120f;
        }

        [SerializeField] private Transform viewer;

        [SerializeField] private List<Chunk> chunks = new();

        [Tooltip("Chunks within this distance of their edge are loaded.")]
        [SerializeField, Min(0f)] private float loadPadding = 60f;

        [Tooltip("Extra distance past loadPadding before a chunk unloads. Must be > 0 or chunks thrash at the boundary.")]
        [SerializeField, Min(10f)] private float unloadHysteresis = 80f;

        [Tooltip("Seconds between streaming checks. Per-frame is pure waste; the player cannot cross a chunk in 0.25s.")]
        [SerializeField, Min(0.05f)] private float evaluateInterval = 0.25f;

        private readonly HashSet<string> _loaded = new();
        private readonly HashSet<string> _inFlight = new();

        private void Awake()
        {
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        }

        public void SetViewer(Transform newViewer) => viewer = newViewer;

        private IEnumerator Start()
        {
            var wait = new WaitForSeconds(evaluateInterval);
            while (enabled)
            {
                Evaluate();
                yield return wait;
            }
        }

        private void Evaluate()
        {
            if (viewer == null) return;

            Vector3 position = viewer.position;

            foreach (Chunk chunk in chunks)
            {
                if (string.IsNullOrEmpty(chunk.SceneName) || _inFlight.Contains(chunk.SceneName)) continue;

                float distance = Vector3.Distance(position, chunk.Centre) - chunk.Radius;
                bool loaded = _loaded.Contains(chunk.SceneName);

                if (!loaded && distance <= loadPadding) StartCoroutine(LoadChunk(chunk.SceneName));
                else if (loaded && distance > loadPadding + unloadHysteresis) StartCoroutine(UnloadChunk(chunk.SceneName));
            }
        }

        private IEnumerator LoadChunk(string sceneName)
        {
            _inFlight.Add(sceneName);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                // Almost always a scene missing from Build Settings, which
                // otherwise fails silently and leaves a hole in the world.
                Debug.LogError($"{nameof(WorldStreamer)}: could not load '{sceneName}'. Is it in Build Settings?", this);
                _inFlight.Remove(sceneName);
                yield break;
            }

            yield return operation;

            _loaded.Add(sceneName);
            _inFlight.Remove(sceneName);
        }

        private IEnumerator UnloadChunk(string sceneName)
        {
            _inFlight.Add(sceneName);

            AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation != null) yield return operation;

            _loaded.Remove(sceneName);
            _inFlight.Remove(sceneName);
        }

        private void OnDrawGizmosSelected()
        {
            foreach (Chunk chunk in chunks)
            {
                bool loaded = Application.isPlaying && _loaded.Contains(chunk.SceneName);
                Gizmos.color = loaded ? new Color(0.2f, 1f, 0.4f, 0.7f) : new Color(1f, 1f, 1f, 0.25f);
                Gizmos.DrawWireSphere(chunk.Centre, chunk.Radius);
            }
        }
    }
}

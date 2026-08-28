using UnityEngine;

namespace Game.Round
{
    /// <summary>A region the spike may be planted in. Put a trigger box on it.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BombSite : MonoBehaviour
    {
        [SerializeField] private string siteName = "A";

        private BoxCollider _box;

        public string SiteName => siteName;

        private void Awake() => _box = GetComponent<BoxCollider>();

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        public bool Contains(Vector3 worldPoint)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            return _box != null && _box.bounds.Contains(worldPoint);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.14f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

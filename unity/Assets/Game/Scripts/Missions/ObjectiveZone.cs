using System;
using UnityEngine;

namespace Game.Missions
{
    /// <summary>
    /// A trigger volume the mission director listens to. Put one on any collider
    /// marked as a trigger and give it the id an objective references.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ObjectiveZone : MonoBehaviour
    {
        [Tooltip("Matched against ReachZoneObjective.ZoneId / ExtractObjective.ZoneId.")]
        [SerializeField] private string zoneId = "zone";

        [Tooltip("Only objects with this tag trigger the zone.")]
        [SerializeField] private string requiredTag = "Player";

        public string ZoneId => zoneId;

        /// <summary>Raised with (zoneId, isInside).</summary>
        public event Action<string, bool> OccupancyChanged;

        private void Reset()
        {
            // A zone that is not a trigger silently blocks the player instead of
            // detecting them, and it looks identical in the inspector.
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(requiredTag)) return;
            OccupancyChanged?.Invoke(zoneId, true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(requiredTag)) return;
            OccupancyChanged?.Invoke(zoneId, false);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.18f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

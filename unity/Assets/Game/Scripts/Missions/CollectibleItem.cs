using System;
using UnityEngine;

namespace Game.Missions
{
    /// <summary>Something the player walks into to pick up.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CollectibleItem : MonoBehaviour
    {
        [Tooltip("Matched against CollectObjective.ItemId.")]
        [SerializeField] private string itemId = "intel";

        [SerializeField] private string requiredTag = "Player";

        [Tooltip("Optional effect spawned where the item was.")]
        [SerializeField] private GameObject pickupEffect;

        /// <summary>Raised with the item id when collected.</summary>
        public static event Action<string> Collected;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(requiredTag)) return;

            if (pickupEffect != null) Instantiate(pickupEffect, transform.position, Quaternion.identity);

            Collected?.Invoke(itemId);
            gameObject.SetActive(false);
        }
    }
}

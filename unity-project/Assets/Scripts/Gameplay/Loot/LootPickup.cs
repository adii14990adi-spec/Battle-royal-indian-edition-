using System;
using UnityEngine;

namespace BattleHorizons.Gameplay.Loot
{
    /// <summary>
    /// One item instance lying in the world. Collection is trigger-based: any
    /// collider tagged "Player" that enters picks the item up and the static
    /// <see cref="Collected"/> event tells the inventory/HUD layer what happened
    /// (the inventory itself is a later milestone; this is the contract it plugs into).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootPickup : MonoBehaviour
    {
        /// <summary>Fired after a successful pickup, with the collected item.</summary>
        public static event Action<LootPickup> Collected;

        public ItemDefinition Item { get; private set; }
        public int Count { get; private set; }

        [SerializeField] private bool destroyOnCollect = true;

        public void Configure(ItemDefinition item, int count)
        {
            Item = item;
            Count = Mathf.Max(1, count);
            if (item != null)
            {
                name = "Pickup_" + item.itemId;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            if (TryCollect() && destroyOnCollect)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Hands the item to whoever listens. Returns false for an empty pickup
        /// so misconfigured spawners never delete themselves silently.
        /// </summary>
        public bool TryCollect()
        {
            if (Item == null)
            {
                return false;
            }

            Collected?.Invoke(this);
            return true;
        }
    }
}

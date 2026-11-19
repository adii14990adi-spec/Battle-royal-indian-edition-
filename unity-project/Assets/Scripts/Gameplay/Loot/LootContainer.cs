using System.Collections.Generic;
using UnityEngine;
using BattleHorizons.World;

namespace BattleHorizons.Gameplay.Loot
{
    /// <summary>
    /// Marks one loot volume — a ProBuilder house, a warehouse crate row, a
    /// watchtower floor. The <see cref="LootSpawner"/> walks the static
    /// <see cref="Registry"/> (no FindObjectOfType scene scans) and rolls loot
    /// inside <see cref="WorldBounds"/>.
    ///
    /// Setup: drop this on the building root (or an empty "LootVolume" child),
    /// size the bounds with the inspector values or a BoxCollider, and the zone
    /// tier rule is resolved automatically from the world position via
    /// <see cref="SiranMapConfig.ZoneAt"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootContainer : MonoBehaviour
    {
        [Header("Bounds source")]
        [Tooltip("When true and a collider exists, its world bounds define the loot volume.")]
        [SerializeField] private bool useColliderBounds = true;
        [Tooltip("Fallback volume in local space (used when useColliderBounds is off or no collider).")]
        [SerializeField] private Vector3 localCenter = Vector3.zero;
        [SerializeField] private Vector3 localSize = new Vector3(6f, 3f, 6f);

        [Header("Spawning")]
        [Tooltip("0 = follow the zone rule; >0 overrides how many rolls this container gets.")]
        [SerializeField] private int fixedDropCount;

        private static readonly List<LootContainer> Registry_ = new List<LootContainer>(256);

        /// <summary>Every enabled container in the loaded scene, in enable order.</summary>
        public static IReadOnlyList<LootContainer> Registry => Registry_;

        private string _zoneId;
        private bool _zoneResolved;

        /// <summary>SiranMapConfig zone id this container sits in ("" when outside all zones).</summary>
        public string ZoneId
        {
            get
            {
                if (!_zoneResolved)
                {
                    var center = WorldBounds.center;
                    var zone = SiranMapConfig.ZoneAt(center.x, center.z);
                    _zoneId = zone != null ? zone.Id : string.Empty;
                    _zoneResolved = true;
                }

                return _zoneId;
            }
        }

        public int FixedDropCount => Mathf.Max(0, fixedDropCount);

        public Bounds WorldBounds
        {
            get
            {
                if (useColliderBounds && TryGetComponent(out Collider collider))
                {
                    return collider.bounds;
                }

                return new Bounds(transform.TransformPoint(localCenter), localSize);
            }
        }

        private void OnEnable()
        {
            Registry_.Add(this);
            _zoneResolved = false;
        }

        private void OnDisable()
        {
            Registry_.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            var bounds = WorldBounds;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}

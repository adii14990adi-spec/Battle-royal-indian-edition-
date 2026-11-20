using UnityEngine;
using BattleHorizons.Core;
using BattleHorizons.World;

namespace BattleHorizons.Gameplay.Match
{
    /// <summary>
    /// Scene-side wrapper around <see cref="PlayerSpawnService"/>: owns the
    /// match seed, materialises the 50-point drop ring, places the local
    /// player, and draws the ring in the Scene view for level review.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnController : MonoBehaviour
    {
        private const string Tag = "spawn";

        [Header("Match")]
        [Tooltip("Match seed — share it with LootSpawner/SafeZoneController for one reproducible match.")]
        [SerializeField] private int matchSeed = 20261119;
        [Range(2, 50)]
        [SerializeField] private int playerCount = PlayerSpawnService.DefaultSpawnPointCount;

        [Header("Placement")]
        [Tooltip("Prefab instantiated at the chosen drop point; must be tagged 'Player'.")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private bool spawnLocalPlayerOnStart = true;
        [Tooltip("Load the committed Siran Island height grid once for exact terrain heights.")]
        [SerializeField] private bool useIslandHeights = true;

        private SiranIslandData _island;

        private Vector3[] _points = new Vector3[0];
        private GameObject _playerInstance;

        public Vector3[] Points => _points;
        public GameObject PlayerInstance => _playerInstance;

        private void Start()
        {
            GeneratePoints();
            if (spawnLocalPlayerOnStart && playerPrefab != null)
            {
                SpawnLocalPlayer(PickDropIndex(matchSeed));
            }
        }

        public void GeneratePoints()
        {
            if (useIslandHeights && _island == null)
            {
                if (!SiranIslandData.TryLoad(out _island, out var error))
                {
                    GameLog.Warn(Tag, $"island heights unavailable ({error}) — using greybox zone estimates");
                    _island = null;
                }
            }

            _points = PlayerSpawnService.GenerateEdgeSpawnPoints(matchSeed, playerCount, _island);
            GameLog.Info(Tag, $"drop ring ready: {_points.Length} points, seed {matchSeed}");
        }

        /// <summary>Deterministic drop-index pick, so the same seed lands the player in the same place.</summary>
        public int PickDropIndex(int seed)
        {
            if (_points.Length == 0)
            {
                GeneratePoints();
            }

            var rng = new RandomStream(unchecked(seed * 31 + 7));
            return rng.Range(0, _points.Length - 1);
        }

        public GameObject SpawnLocalPlayer(int index)
        {
            if (playerPrefab == null)
            {
                GameLog.Warn(Tag, "no playerPrefab assigned — nothing to spawn");
                return null;
            }

            if (_points.Length == 0)
            {
                GeneratePoints();
            }

            index = Mathf.Clamp(index, 0, _points.Length - 1);
            if (_playerInstance != null)
            {
                Destroy(_playerInstance);
            }

            _playerInstance = Instantiate(playerPrefab, _points[index], Quaternion.identity);
            _playerInstance.name = "Player_Local";
            GameLog.Info(Tag, $"local player dropped at point {index} {_points[index]}");
            return _playerInstance;
        }

        private void OnDrawGizmos()
        {
            if (_points == null || _points.Length == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            for (var i = 0; i < _points.Length; i++)
            {
                Gizmos.DrawWireSphere(_points[i], 2f);
            }
        }
    }
}

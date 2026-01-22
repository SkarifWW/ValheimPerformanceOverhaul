using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
    public class DistanceCullerManager : MonoBehaviour
    {
        public static DistanceCullerManager Instance { get; private set; }

        private static readonly List<Player> _globalPlayerCache = new List<Player>();
        public static IReadOnlyList<Player> Players => _globalPlayerCache;

        private float _updateTimer;
        private const float UPDATE_INTERVAL = 1.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Plugin.Log.LogInfo("[DistanceCullerManager] Initialized.");
        }

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            // Обновляем список игроков ОДИН раз за секунду для ВСЕХ DistanceCuller
            _globalPlayerCache.Clear();
            var players = Player.GetAllPlayers();
            if (players != null)
            {
                _globalPlayerCache.AddRange(players);
            }
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}
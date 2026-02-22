using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
                                                    public class DistanceCullerManager : MonoBehaviour
    {
        public static DistanceCullerManager Instance { get; private set; }

                        private static readonly List<Player> _globalPlayerCache = new List<Player>();
        public static IReadOnlyList<Player> Players => _globalPlayerCache;

                private readonly List<DistanceCuller> _cullers = new List<DistanceCuller>(512);

        private float _playerUpdateTimer;
        private float _cullingUpdateTimer;

                private const float PLAYER_UPDATE_INTERVAL = 1.0f;

                private const float CULLING_UPDATE_INTERVAL = 2.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Plugin.Log.LogInfo("[DistanceCullerManager] Initialized.");
        }

        public void RegisterCuller(DistanceCuller culler)
        {
            if (culler != null && !_cullers.Contains(culler))
                _cullers.Add(culler);
        }

        public void UnregisterCuller(DistanceCuller culler)
        {
                                    int idx = _cullers.IndexOf(culler);
            if (idx < 0) return;
            int last = _cullers.Count - 1;
            _cullers[idx] = _cullers[last];
            _cullers.RemoveAt(last);
        }

        private void Update()
        {
            _playerUpdateTimer += Time.deltaTime;
            _cullingUpdateTimer += Time.deltaTime;

                        if (_playerUpdateTimer >= PLAYER_UPDATE_INTERVAL)
            {
                _playerUpdateTimer = 0f;
                _globalPlayerCache.Clear();
                var players = Player.GetAllPlayers();
                if (players != null)
                    _globalPlayerCache.AddRange(players);
            }

                        if (_cullingUpdateTimer >= CULLING_UPDATE_INTERVAL)
            {
                _cullingUpdateTimer = 0f;

                if (!Plugin.DistanceCullerEnabled.Value) return;
                if (_globalPlayerCache.Count == 0) return;

                                for (int i = _cullers.Count - 1; i >= 0; i--)
                {
                    var culler = _cullers[i];

                                        if (culler == null)
                    {
                        int last = _cullers.Count - 1;
                        _cullers[i] = _cullers[last];
                        _cullers.RemoveAt(last);
                        continue;
                    }

                    culler.ManagerUpdate(_globalPlayerCache);
                }
            }
        }

        private void OnDestroy()
        {
            _cullers.Clear();
            Instance = null;
        }
    }
}
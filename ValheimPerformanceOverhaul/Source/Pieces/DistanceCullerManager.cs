using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
    // =========================================================================
    // КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ:
    //
    // Было: каждый DistanceCuller (1 на каждый блок постройки и каждую тварь)
    //       имел собственный MonoBehaviour.Update(). На 300 блоках — 300 вызовов
    //       Update() каждый кадр. Unity платит за каждый вызов overhead на
    //       вызов через UnityEngine → C# interop, даже если метод пустой.
    //
    // Стало: DistanceCuller.Update() удалён полностью. Все кулеры регистрируются
    //        в DistanceCullerManager и обновляются в ОДНОМ цикле раз в N секунд.
    //        300 блоков = 1 Update() вместо 300.
    // =========================================================================
    public class DistanceCullerManager : MonoBehaviour
    {
        public static DistanceCullerManager Instance { get; private set; }

        // Список игроков — общий кэш, чтобы DistanceCuller не вызывал
        // Player.GetAllPlayers() сам по себе.
        private static readonly List<Player> _globalPlayerCache = new List<Player>();
        public static IReadOnlyList<Player> Players => _globalPlayerCache;

        // Список всех зарегистрированных кулеров — центральный реестр.
        private readonly List<DistanceCuller> _cullers = new List<DistanceCuller>(512);

        private float _playerUpdateTimer;
        private float _cullingUpdateTimer;

        // Игроки обновляются часто — они движутся.
        private const float PLAYER_UPDATE_INTERVAL = 1.0f;

        // Куллинг обновляется реже — большинство объектов статичны.
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
            // RemoveSwapBack: меняем удаляемый элемент с последним и уменьшаем Count.
            // O(1) вместо O(N) сдвига — важно при большом списке.
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

            // Обновляем кэш игроков.
            if (_playerUpdateTimer >= PLAYER_UPDATE_INTERVAL)
            {
                _playerUpdateTimer = 0f;
                _globalPlayerCache.Clear();
                var players = Player.GetAllPlayers();
                if (players != null)
                    _globalPlayerCache.AddRange(players);
            }

            // Обновляем куллинг всех объектов в одном цикле.
            if (_cullingUpdateTimer >= CULLING_UPDATE_INTERVAL)
            {
                _cullingUpdateTimer = 0f;

                if (!Plugin.DistanceCullerEnabled.Value) return;
                if (_globalPlayerCache.Count == 0) return;

                // Удаляем мёртвые записи + обновляем живые за один проход.
                for (int i = _cullers.Count - 1; i >= 0; i--)
                {
                    var culler = _cullers[i];

                    // Кулер уничтожен — удаляем SwapBack.
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
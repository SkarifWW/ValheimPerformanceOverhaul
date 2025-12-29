using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Collections.Concurrent; // ✅ НОВОЕ

namespace ValheimPerformanceOverhaul.Pieces
{
    // ПАТЧ 1: Throttling обновлений WearNTear
    [HarmonyPatch(typeof(WearNTear), "UpdateWear")]
    public static class WearNTear_UpdateWear_Patch
    {
        // ✅ ИСПРАВЛЕНО: Thread-safe dictionary
        private static readonly ConcurrentDictionary<WearNTear, float> _updateTimers =
            new ConcurrentDictionary<WearNTear, float>();

        private static float _lastCheckTime = 0f;

        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            // Пропускаем для далеких объектов
            if (Player.m_localPlayer != null)
            {
                float distSqr = Vector3.Distance(__instance.transform.position, Player.m_localPlayer.transform.position);

                if (distSqr > Plugin.PieceUpdateSkipDistance.Value)
                {
                    return false;
                }
            }

            // Throttling: обновляем не каждый кадр
            float currentTime = Time.time;
            float timeSinceLastCheck = currentTime - _lastCheckTime;
            _lastCheckTime = currentTime;

            float timer = _updateTimers.GetOrAdd(__instance, Random.Range(0f, Plugin.PieceUpdateInterval.Value));
            timer += timeSinceLastCheck;

            if (timer < Plugin.PieceUpdateInterval.Value)
            {
                _updateTimers[__instance] = timer;
                return false;
            }

            _updateTimers[__instance] = 0f;
            return true;
        }

        // ✅ НОВОЕ: Периодическая очистка мертвых ссылок
        [HarmonyPatch(typeof(WearNTear), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy_Postfix(WearNTear __instance)
        {
            _updateTimers.TryRemove(__instance, out _);
        }
    }

    // ПАТЧ 2: Кэширование поддержки
    [HarmonyPatch(typeof(WearNTear), "GetSupport")]
    public static class WearNTear_GetSupport_Patch
    {
        // ✅ ИСПРАВЛЕНО: Thread-safe dictionary
        private static readonly ConcurrentDictionary<WearNTear, CachedSupport> _supportCache =
            new ConcurrentDictionary<WearNTear, CachedSupport>();

        private class CachedSupport
        {
            public float Value;
            public float Timestamp;
        }

        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance, ref float __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            if (_supportCache.TryGetValue(__instance, out CachedSupport cache))
            {
                if (Time.time - cache.Timestamp < Plugin.PieceSupportCacheDuration.Value)
                {
                    __result = cache.Value;
                    return false;
                }
            }

            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(WearNTear __instance, float __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;

            var cache = _supportCache.GetOrAdd(__instance, new CachedSupport());
            cache.Value = __result;
            cache.Timestamp = Time.time;
        }

        [HarmonyPatch(typeof(WearNTear), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy_Postfix(WearNTear __instance)
        {
            _supportCache.TryRemove(__instance, out _);
        }
    }

    // ПАТЧ 3: Оптимизация Piece.IsPlacedByPlayer()
    [HarmonyPatch(typeof(Piece), nameof(Piece.IsPlacedByPlayer))]
    public static class Piece_IsPlacedByPlayer_Patch
    {
        // ✅ ИСПРАВЛЕНО: Thread-safe dictionary
        private static readonly ConcurrentDictionary<Piece, bool> _placedByPlayerCache =
            new ConcurrentDictionary<Piece, bool>();

        [HarmonyPrefix]
        private static bool Prefix(Piece __instance, ref bool __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            if (_placedByPlayerCache.TryGetValue(__instance, out bool cached))
            {
                __result = cached;
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(Piece __instance, bool __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;
            _placedByPlayerCache.TryAdd(__instance, __result);
        }

        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy_Postfix(Piece __instance)
        {
            _placedByPlayerCache.TryRemove(__instance, out _);
        }
    }

    // ПАТЧ 4: Оптимизация Piece.GetCreator()
    [HarmonyPatch(typeof(Piece), nameof(Piece.GetCreator))]
    public static class Piece_GetCreator_Patch
    {
        // ✅ ИСПРАВЛЕНО: Thread-safe dictionary
        private static readonly ConcurrentDictionary<Piece, long> _creatorCache =
            new ConcurrentDictionary<Piece, long>();

        [HarmonyPrefix]
        private static bool Prefix(Piece __instance, ref long __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            if (_creatorCache.TryGetValue(__instance, out long cached))
            {
                __result = cached;
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(Piece __instance, long __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;
            _creatorCache.TryAdd(__instance, __result);
        }

        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy_Postfix(Piece __instance)
        {
            _creatorCache.TryRemove(__instance, out _);
        }
    }

    // ПАТЧ 5: Collider Manager
    public class PieceColliderManager : MonoBehaviour
    {
        public static PieceColliderManager Instance { get; private set; }

        private readonly List<ColliderInfo> _allPieceColliders = new List<ColliderInfo>();
        private readonly HashSet<Collider> _disabledColliders = new HashSet<Collider>();

        private class ColliderInfo
        {
            public Collider Collider;
            public Transform Transform;
            public bool IsKinematic;
        }

        private float _updateTimer;
        private const float UPDATE_INTERVAL = 1.0f;
        private int _cleanupCounter = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Plugin.Log.LogInfo("[PieceOptimization] Collider Manager initialized.");
        }

        public void RegisterCollider(Collider collider)
        {
            if (collider == null) return;

            // Проверяем дубликаты
            foreach (var info in _allPieceColliders)
            {
                if (info.Collider == collider) return;
            }

            var rb = collider.attachedRigidbody;
            bool isKinematic = rb != null && rb.isKinematic;

            _allPieceColliders.Add(new ColliderInfo
            {
                Collider = collider,
                Transform = collider.transform,
                IsKinematic = isKinematic
            });
        }

        private void Update()
        {
            if (!Plugin.PieceOptimizationEnabled.Value || Player.m_localPlayer == null)
                return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            // ✅ ИСПРАВЛЕНО: Ленивая очистка (каждые 10 обновлений)
            _cleanupCounter++;
            if (_cleanupCounter >= 10)
            {
                _cleanupCounter = 0;
                CleanupNullColliders();
            }

            UpdateColliderStates();
        }

        // ✅ НОВОЕ: Оптимизированная очистка
        private void CleanupNullColliders()
        {
            for (int i = _allPieceColliders.Count - 1; i >= 0; i--)
            {
                if (_allPieceColliders[i].Collider == null)
                {
                    _allPieceColliders.RemoveAt(i);
                }
            }
        }

        private void UpdateColliderStates()
        {
            Vector3 playerPos = Player.m_localPlayer.transform.position;
            float disableDistSqr = Plugin.PieceColliderDistance.Value * Plugin.PieceColliderDistance.Value;

            foreach (var info in _allPieceColliders)
            {
                if (info.Collider == null) continue;

                float distSqr = (info.Transform.position - playerPos).sqrMagnitude;
                bool shouldBeDisabled = distSqr > disableDistSqr;

                if (shouldBeDisabled && info.Collider.enabled)
                {
                    if (info.IsKinematic)
                    {
                        info.Collider.enabled = false;
                        _disabledColliders.Add(info.Collider);
                    }
                }
                else if (!shouldBeDisabled && !info.Collider.enabled && _disabledColliders.Contains(info.Collider))
                {
                    info.Collider.enabled = true;
                    _disabledColliders.Remove(info.Collider);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var collider in _disabledColliders)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
            _disabledColliders.Clear();
            Instance = null;
        }
    }

    // ПАТЧ 6: Регистрация коллайдеров
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class Piece_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;

            if (PieceColliderManager.Instance == null)
            {
                var manager = new GameObject("_VPO_PieceColliderManager");
                manager.AddComponent<PieceColliderManager>();
            }

            var colliders = __instance.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                if (collider != null)
                {
                    PieceColliderManager.Instance?.RegisterCollider(collider);
                }
            }
        }
    }
}
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace ValheimPerformanceOverhaul.Pieces
{
    // ПАТЧ 1: Throttling обновлений WearNTear через UpdateWear (реальный метод)
    [HarmonyPatch(typeof(WearNTear), "UpdateWear")]
    public static class WearNTear_UpdateWear_Patch
    {
        private static readonly Dictionary<WearNTear, float> _updateTimers = new Dictionary<WearNTear, float>();
        private static float _lastCheckTime = 0f;

        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            // Пропускаем для далеких объектов
            if (Player.m_localPlayer != null)
            {
                float distSqr = Vector3.Distance(__instance.transform.position, Player.m_localPlayer.transform.position);

                // За пределами UpdateSkipDistance не обновляем вообще
                if (distSqr > Plugin.PieceUpdateSkipDistance.Value)
                {
                    return false;
                }
            }

            // Throttling: обновляем не каждый кадр
            if (!_updateTimers.TryGetValue(__instance, out float timer))
            {
                timer = Random.Range(0f, Plugin.PieceUpdateInterval.Value);
                _updateTimers[__instance] = timer;
            }

            float currentTime = Time.time;
            float timeSinceLastCheck = currentTime - _lastCheckTime;
            _lastCheckTime = currentTime;

            timer += timeSinceLastCheck;

            if (timer < Plugin.PieceUpdateInterval.Value)
            {
                _updateTimers[__instance] = timer;
                return false; // Пропускаем этот вызов
            }

            _updateTimers[__instance] = 0f;
            return true; // Выполняем оригинальный UpdateWear
        }
    }

    // ПАТЧ 2: Кэширование поддержки (support calculation)
    [HarmonyPatch(typeof(WearNTear), "GetSupport")]
    public static class WearNTear_GetSupport_Patch
    {
        private static readonly Dictionary<WearNTear, CachedSupport> _supportCache =
            new Dictionary<WearNTear, CachedSupport>();

        private class CachedSupport
        {
            public float Value;
            public float Timestamp;
        }

        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance, ref float __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            // Проверяем кэш
            if (_supportCache.TryGetValue(__instance, out CachedSupport cache))
            {
                if (Time.time - cache.Timestamp < Plugin.PieceSupportCacheDuration.Value)
                {
                    __result = cache.Value;
                    return false; // Используем кэшированное значение
                }
            }

            return true; // Пересчитываем
        }

        [HarmonyPostfix]
        private static void Postfix(WearNTear __instance, float __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;

            // Сохраняем в кэш
            if (!_supportCache.ContainsKey(__instance))
            {
                _supportCache[__instance] = new CachedSupport();
            }

            _supportCache[__instance].Value = __result;
            _supportCache[__instance].Timestamp = Time.time;
        }

        // Очистка кэша при уничтожении
        [HarmonyPatch(typeof(WearNTear), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy_Postfix(WearNTear __instance)
        {
            _supportCache.Remove(__instance);
        }
    }

    // ПАТЧ 3: Отключение ненужных Collider на далеких постройках
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

            // Проверяем, не зарегистрирован ли уже
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
            {
                return;
            }

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            UpdateColliderStates();
        }

        private void UpdateColliderStates()
        {
            Vector3 playerPos = Player.m_localPlayer.transform.position;
            float disableDistSqr = Plugin.PieceColliderDistance.Value * Plugin.PieceColliderDistance.Value;

            // Очистка null-коллайдеров
            _allPieceColliders.RemoveAll(info => info.Collider == null);

            foreach (var info in _allPieceColliders)
            {
                if (info.Collider == null) continue;

                float distSqr = (info.Transform.position - playerPos).sqrMagnitude;
                bool shouldBeDisabled = distSqr > disableDistSqr;

                if (shouldBeDisabled && info.Collider.enabled)
                {
                    // Отключаем только статические коллайдеры
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
            // Восстанавливаем все коллайдеры
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

    // ПАТЧ 4: Регистрация коллайдеров при создании построек
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class Piece_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;

            // Создаем менеджер при первой необходимости
            if (PieceColliderManager.Instance == null)
            {
                var manager = new GameObject("_VPO_PieceColliderManager");
                manager.AddComponent<PieceColliderManager>();
            }

            // Регистрируем все коллайдеры этой постройки
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

    // ПАТЧ 5: Оптимизация Piece.IsPlacedByPlayer() - вызывается слишком часто
    [HarmonyPatch(typeof(Piece), nameof(Piece.IsPlacedByPlayer))]
    public static class Piece_IsPlacedByPlayer_Patch
    {
        private static readonly Dictionary<Piece, bool> _placedByPlayerCache = new Dictionary<Piece, bool>();

        [HarmonyPrefix]
        private static bool Prefix(Piece __instance, ref bool __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            // Это значение не меняется после создания, поэтому кэшируем навсегда
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

            if (!_placedByPlayerCache.ContainsKey(__instance))
            {
                _placedByPlayerCache[__instance] = __result;
            }
        }

        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy_Postfix(Piece __instance)
        {
            _placedByPlayerCache.Remove(__instance);
        }
    }

    // ПАТЧ 6: Оптимизация Piece.GetCreator() - тоже часто вызывается
    [HarmonyPatch(typeof(Piece), nameof(Piece.GetCreator))]
    public static class Piece_GetCreator_Patch
    {
        private static readonly Dictionary<Piece, long> _creatorCache = new Dictionary<Piece, long>();

        [HarmonyPrefix]
        private static bool Prefix(Piece __instance, ref long __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            // Кэшируем создателя
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

            if (!_creatorCache.ContainsKey(__instance))
            {
                _creatorCache[__instance] = __result;
            }
        }

        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy_Postfix(Piece __instance)
        {
            _creatorCache.Remove(__instance);
        }
    }
} 
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.AI
{
    [HarmonyPatch]
    public static class AIPatches
    {
        // === МОДУЛЬ 1: AI АКТИВАЦИЯ ПО ДИСТАНЦИИ ===
        private const float AI_ACTIVATION_RADIUS = 120f;
        private const float AI_DEACTIVATION_RADIUS = 140f; // Hysteresis

        // ✅ НОВОЕ: Slow update для далеких мобов (раз в 5 секунд)
        private const float AI_SLOW_UPDATE_INTERVAL = 5f;
        private static readonly Dictionary<BaseAI, float> _lastSlowUpdate = new Dictionary<BaseAI, float>();

        [HarmonyPatch(typeof(BaseAI), "Awake")]
        [HarmonyPostfix]
        private static void BaseAI_Awake_Postfix(BaseAI __instance)
        {
            if (__instance.GetComponent<AIOptimizer>() == null)
            {
                __instance.gameObject.AddComponent<AIOptimizer>();
            }

            // ✅ НОВОЕ: Добавляем TamedIdleOptimizer для прирученных мобов
            var character = __instance.GetComponent<Character>();
            if (character != null && character.IsTamed())
            {
                if (__instance.GetComponent<TamedIdleOptimizer>() == null)
                {
                    __instance.gameObject.AddComponent<TamedIdleOptimizer>();
                }
            }

            // ✅ НОВОЕ: Инициализируем slow update timer
            _lastSlowUpdate[__instance] = Time.time;
        }

        // ✅ ИСПРАВЛЕНО: MonsterAI.UpdateAI - главная проверка дистанции
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        private static bool MonsterAI_UpdateAI_Prefix(MonsterAI __instance, float dt)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

            // Только на сервере или в синглплеере
            if (ZNet.instance != null && !ZNet.instance.IsServer()) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer == null) return true;

            float distToPlayer = optimizer.GetDistanceToPlayer();

            // ✅ ИСПРАВЛЕНО: Далекие мобы получают МЕДЛЕННЫЙ update
            if (distToPlayer > AI_ACTIVATION_RADIUS)
            {
                // Проверяем, прошло ли достаточно времени для slow update
                if (!_lastSlowUpdate.TryGetValue(__instance, out float lastUpdate))
                {
                    _lastSlowUpdate[__instance] = Time.time;
                    return false;
                }

                float timeSinceLastUpdate = Time.time - lastUpdate;

                if (timeSinceLastUpdate < AI_SLOW_UPDATE_INTERVAL)
                {
                    return false; // Пропускаем этот update
                }

                // ✅ КРИТИЧНО: Обновляем раз в 5 секунд для:
                // - Возврата домой
                // - Проверки патруля
                // - Обновления состояния
                _lastSlowUpdate[__instance] = Time.time;

                if (Plugin.DebugLoggingEnabled.Value && Time.frameCount % 300 == 0)
                {
                    Plugin.Log.LogInfo($"[AI] Slow update for {__instance.name} at {distToPlayer:F1}m");
                }

                return true; // Выполняем МЕДЛЕННЫЙ update
            }

            // ✅ Близкие мобы - нормальный update
            return true;
        }

        // ✅ НОВОЕ: Очистка при уничтожении
        [HarmonyPatch(typeof(BaseAI), "OnDestroy")]
        [HarmonyPostfix]
        private static void BaseAI_OnDestroy_Postfix(BaseAI __instance)
        {
            _lastSlowUpdate.Remove(__instance);
        }

        // === МОДУЛЬ: ПОИСК ВРАГОВ (кеширование) ===
        [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
        [HarmonyPrefix]
        private static bool FindEnemy_Prefix(BaseAI __instance, ref Character __result)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null && !optimizer.ShouldCheckEnemy(out __result))
            {
                return false; // Используем кешированного врага
            }
            return true;
        }

        [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
        [HarmonyPostfix]
        private static void FindEnemy_Postfix(BaseAI __instance, Character __result)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null)
            {
                optimizer.UpdateCachedEnemy(__result);
            }
        }

        // === МОДУЛЬ 4: LINE OF SIGHT (Raycast оптимизация) ===
        [HarmonyPatch(typeof(BaseAI), "CanSeeTarget", new System.Type[] { typeof(Character) })]
        [HarmonyPrefix]
        private static bool CanSeeTarget_Prefix(BaseAI __instance, Character target, ref bool __result)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null)
            {
                // Проверяем кеш
                if (optimizer.GetCachedLOS(target, out bool cachedVisible))
                {
                    __result = cachedVisible;
                    return false;
                }

                // ✅ ОПТИМИЗАЦИЯ: Если игрок очень далеко, LOS всегда false без raycast
                float distToPlayer = optimizer.GetDistanceToPlayer();
                if (distToPlayer > 40f)
                {
                    __result = false;
                    optimizer.SetCachedLOS(target, false);
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch(typeof(BaseAI), "CanSeeTarget", new System.Type[] { typeof(Character) })]
        [HarmonyPostfix]
        private static void CanSeeTarget_Postfix(BaseAI __instance, Character target, bool __result)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null && target != null)
            {
                optimizer.SetCachedLOS(target, __result);
            }
        }

        // === МОДУЛЬ 6: ЛОГИКА БОЯ (cooldown на атаки) ===
        [HarmonyPatch(typeof(MonsterAI), "DoAttack")]
        [HarmonyPrefix]
        private static bool DoAttack_Prefix(MonsterAI __instance)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null)
            {
                return optimizer.ShouldCheckAttack();
            }
            return true;
        }

        // ✅ НОВОЕ: Периодическая очистка slow update cache
        [HarmonyPatch(typeof(ZNet), "Update")]
        [HarmonyPostfix]
        private static void CleanupSlowUpdateCache()
        {
            if (!Plugin.AiThrottlingEnabled.Value) return;

            // Очистка раз в минуту
            if (Time.frameCount % 3600 == 0)
            {
                var toRemove = new List<BaseAI>();
                float currentTime = Time.time;

                foreach (var kvp in _lastSlowUpdate)
                {
                    if (kvp.Key == null || currentTime - kvp.Value > 60f)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var ai in toRemove)
                {
                    _lastSlowUpdate.Remove(ai);
                }

                if (Plugin.DebugLoggingEnabled.Value && toRemove.Count > 0)
                {
                    Plugin.Log.LogInfo($"[AI] Cleaned {toRemove.Count} stale slow update entries");
                }
            }
        }
    }
}
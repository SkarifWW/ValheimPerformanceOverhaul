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

        [HarmonyPatch(typeof(BaseAI), "Awake")]
        [HarmonyPostfix]
        private static void BaseAI_Awake_Postfix(BaseAI __instance)
        {
            if (__instance.GetComponent<AIOptimizer>() == null)
            {
                __instance.gameObject.AddComponent<AIOptimizer>();
            }
        }

        // === КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: MonsterAI.UpdateAI - главная проверка дистанции ===
        // ВАЖНО: MonsterAI OVERRIDE BaseAI.UpdateAI, поэтому патчим MonsterAI, а не BaseAI!
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
            
            // Если моб далеко - НЕ обновляем AI
            if (distToPlayer > AI_ACTIVATION_RADIUS)
            {
                return false; // Пропускаем обновление AI для далеких мобов
            }

            return true; // Продолжаем нормальное выполнение
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
                    return false; // Используем кешированный результат
                }

                // Если игрок очень далеко (> 40м), LOS всегда false без raycast
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
    }
}

using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.AI
{
    [HarmonyPatch]
    public static class AIPatches
    {
                                private const float AI_ACTIVATION_RADIUS = 60f;          private const float AI_DEACTIVATION_RADIUS = 80f; 
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

                        var character = __instance.GetComponent<Character>();
            if (character != null && character.IsTamed())
            {
                if (__instance.GetComponent<TamedIdleOptimizer>() == null)
                {
                    __instance.gameObject.AddComponent<TamedIdleOptimizer>();
                }
            }

                        _lastSlowUpdate[__instance] = Time.time;
        }

                [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        private static bool MonsterAI_UpdateAI_Prefix(MonsterAI __instance, float dt)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

                        if (ZNet.instance != null && !ZNet.instance.IsServer()) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer == null) return true;

            float distToPlayer = optimizer.GetDistanceToPlayer();

                                    if (distToPlayer <= AI_ACTIVATION_RADIUS)
            {
                return true;             }

                        if (distToPlayer > AI_ACTIVATION_RADIUS)
            {
                                if (!_lastSlowUpdate.TryGetValue(__instance, out float lastUpdate))
                {
                    _lastSlowUpdate[__instance] = Time.time;
                    return false;
                }

                float timeSinceLastUpdate = Time.time - lastUpdate;

                if (timeSinceLastUpdate < AI_SLOW_UPDATE_INTERVAL)
                {
                    return false;                 }

                                                                                _lastSlowUpdate[__instance] = Time.time;

                if (Plugin.DebugLoggingEnabled.Value && Time.frameCount % 300 == 0)
                {
                    Plugin.Log.LogInfo($"[AI] Slow update for {__instance.name} at {distToPlayer:F1}m");
                }

                return true;             }

                        return true;
        }

                [HarmonyPatch(typeof(BaseAI), "OnDestroy")]
        [HarmonyPostfix]
        private static void BaseAI_OnDestroy_Postfix(BaseAI __instance)
        {
            _lastSlowUpdate.Remove(__instance);
        }

                [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
        [HarmonyPrefix]
        private static bool FindEnemy_Prefix(BaseAI __instance, ref Character __result)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null)
            {
                                                if (optimizer.GetDistanceToPlayer() < AI_ACTIVATION_RADIUS)
                {
                    return true;
                }

                if (!optimizer.ShouldCheckEnemy(out __result))
                {
                    return false;                 }
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

                [HarmonyPatch(typeof(BaseAI), "CanSeeTarget", new System.Type[] { typeof(Character) })]
        [HarmonyPrefix]
        private static bool CanSeeTarget_Prefix(BaseAI __instance, Character target, ref bool __result)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null)
            {
                                 if (optimizer.GetDistanceToPlayer() < AI_ACTIVATION_RADIUS)
                {
                    return true;
                }

                                if (optimizer.GetCachedLOS(target, out bool cachedVisible))
                {
                    __result = cachedVisible;
                    return false;
                }

                                float distToPlayer = optimizer.GetDistanceToPlayer();
                if (distToPlayer > 40f)                 {
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

                [HarmonyPatch(typeof(MonsterAI), "DoAttack")]
        [HarmonyPrefix]
        private static bool DoAttack_Prefix(MonsterAI __instance)
        {
            if (!Plugin.AiThrottlingEnabled.Value) return true;

            var optimizer = __instance.GetComponent<AIOptimizer>();
            if (optimizer != null)
            {
                if (optimizer.GetDistanceToPlayer() < AI_ACTIVATION_RADIUS)
                {
                    return true;
                }
                return optimizer.ShouldCheckAttack();
            }
            return true;
        }

                [HarmonyPatch(typeof(ZNet), "Update")]
        [HarmonyPostfix]
        private static void CleanupSlowUpdateCache()
        {
            if (!Plugin.AiThrottlingEnabled.Value) return;

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
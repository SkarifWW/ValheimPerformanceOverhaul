using HarmonyLib;
using UnityEngine;
using System.Collections.Concurrent;
using ValheimPerformanceOverhaul;

namespace ValheimPerformanceOverhaul.Pieces
{
    public static class SupportCache
    {
        private static readonly ConcurrentDictionary<WearNTear, CachedSupport> _supportCache =
            new ConcurrentDictionary<WearNTear, CachedSupport>();

        private class CachedSupport
        {
            public float Value;
            public float Timestamp;
        }

                private static float CacheDuration => Plugin.PieceSupportCacheDuration.Value;

        [HarmonyPatch(typeof(WearNTear), "GetSupport")]
        [HarmonyPrefix]
        private static bool Prefix(WearNTear __instance, ref float __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;

            if (_supportCache.TryGetValue(__instance, out CachedSupport cache))
            {
                if (Time.time - cache.Timestamp < CacheDuration)
                {
                    __result = cache.Value;
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(WearNTear), "GetSupport")]
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
}

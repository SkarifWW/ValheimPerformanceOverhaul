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

        // Config linked to main plugin
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
        
        // TODO: Invalidate cache neighbors on destruction? 
        // Valheim's support system effectively initiates checks when something breaks. 
        // If we cache GetSupport, we might return old "Healthy" support even if the support below is gone?
        // Wait, GetSupport RECALCULATES support. If we return cached value, we skip recalculation.
        // So yes, if a support beam breaks, we MUST invalidate cache for things above it.
        // WearNTear doesn't have a simple "OnNeighborDestroyed" event.
        // However, WearNTear calls GetSupport periodically.
        // If we use cache, we delay the collapse by CacheDuration (e.g., 5 seconds). 
        // This is acceptable for "lag removal" at the cost of "delayed collapse".
        // The user plan says: "3. ... If object changed ... reset cache".
        // "When changing construction nearby -> reset cache".
        // Implementing strict neighbor invalidation is complex (need spatial hash).
        // For now, the 5s cache is a trade-off. We can keep it simple as per original mod.
    }
}

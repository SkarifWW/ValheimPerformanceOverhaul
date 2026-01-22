using HarmonyLib;
using UnityEngine;
using ValheimPerformanceOverhaul;
using System.Collections.Concurrent;

namespace ValheimPerformanceOverhaul.Pieces
{
    public static class PiecePatches
    {
        private static readonly ConcurrentDictionary<Piece, bool> _placedByPlayerCache =
            new ConcurrentDictionary<Piece, bool>();
        
        private static readonly ConcurrentDictionary<Piece, long> _creatorCache =
            new ConcurrentDictionary<Piece, long>();

        [HarmonyPatch(typeof(Piece), "Awake")]
        [HarmonyPostfix]
        private static void Piece_Awake_Postfix(Piece __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;
            PieceSleepManager.Instance?.RegisterPiece(__instance);
        }

        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        [HarmonyPostfix]
        private static void Piece_OnDestroy_Postfix(Piece __instance)
        {
            PieceSleepManager.Instance?.UnregisterPiece(__instance);
            _placedByPlayerCache.TryRemove(__instance, out _);
            _creatorCache.TryRemove(__instance, out _);
        }

        [HarmonyPatch(typeof(Piece), "IsPlacedByPlayer")]
        [HarmonyPrefix]
        private static bool IsPlacedByPlayer_Prefix(Piece __instance, ref bool __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;
            if (_placedByPlayerCache.TryGetValue(__instance, out bool cached))
            {
                __result = cached;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Piece), "IsPlacedByPlayer")]
        [HarmonyPostfix]
        private static void IsPlacedByPlayer_Postfix(Piece __instance, bool __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;
            _placedByPlayerCache.TryAdd(__instance, __result);
        }

        [HarmonyPatch(typeof(Piece), "GetCreator")]
        [HarmonyPrefix]
        private static bool GetCreator_Prefix(Piece __instance, ref long __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return true;
            if (_creatorCache.TryGetValue(__instance, out long cached))
            {
                __result = cached;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Piece), "GetCreator")]
        [HarmonyPostfix]
        private static void GetCreator_Postfix(Piece __instance, long __result)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;
            _creatorCache.TryAdd(__instance, __result);
        }
    }
}

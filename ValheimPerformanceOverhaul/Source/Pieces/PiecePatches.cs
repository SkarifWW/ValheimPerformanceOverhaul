using HarmonyLib;
using UnityEngine;
using ValheimPerformanceOverhaul;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.Pieces
{
    public static class PiecePatches
    {
        // FIX: ConcurrentDictionary → Dictionary.
        // Все вызовы Piece.IsPlacedByPlayer и Piece.GetCreator идут из main thread —
        // thread-safety не нужна. ConcurrentDictionary использует lock на каждую операцию
        // и в 3-5 раз медленнее обычного Dictionary в single-thread сценарии.
        private static readonly Dictionary<Piece, bool> _placedByPlayerCache =
            new Dictionary<Piece, bool>();

        private static readonly Dictionary<Piece, long> _creatorCache =
            new Dictionary<Piece, long>();

        [HarmonyPatch(typeof(Piece), "Awake")]
        [HarmonyPostfix]
        private static void Piece_Awake_Postfix(Piece __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;
            if (__instance.gameObject.layer == LayerMask.NameToLayer("ghost")) return;

            PieceSleepManager.Instance?.RegisterPiece(__instance);
        }

        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        [HarmonyPostfix]
        private static void Piece_OnDestroy_Postfix(Piece __instance)
        {
            PieceSleepManager.Instance?.UnregisterPiece(__instance);
            _placedByPlayerCache.Remove(__instance);
            _creatorCache.Remove(__instance);
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

            // TryAdd equivalent: only write if key not yet present.
            if (!_placedByPlayerCache.ContainsKey(__instance))
                _placedByPlayerCache[__instance] = __result;
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

            if (!_creatorCache.ContainsKey(__instance))
                _creatorCache[__instance] = __result;
        }
    }
}
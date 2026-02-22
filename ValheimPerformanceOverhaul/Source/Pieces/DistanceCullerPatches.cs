using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Pieces
{
    [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
    public static class DistanceCuller_CreateObject_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ZDO zdo, GameObject __result)
        {
                        if (!Plugin.DistanceCullerEnabled.Value) return;

            if (__result == null) return;

            var netView = __result.GetComponent<ZNetView>();
            if (netView == null || !netView.IsValid()) return;

                        string cleanName = __result.name.Replace("(Clone)", "");
            if (Plugin.CullerExclusions.Value.Contains(cleanName))
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[DistanceCuller] Excluded: {cleanName}");
                return;
            }

                        if (__result.GetComponent<DistanceCuller>() != null)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogWarning($"[DistanceCuller] Already attached to {cleanName}");
                return;
            }

                        bool isCreature = __result.GetComponent<Character>() != null;
            bool isPiece = __result.GetComponent<Piece>() != null;

            if (!isCreature && !isPiece) return;

            var culler = __result.AddComponent<DistanceCuller>();
            culler.CullDistance = isCreature
                ? Plugin.CreatureCullDistance.Value
                : Plugin.PieceCullDistance.Value;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[DistanceCuller] Attached to {cleanName} ({(isCreature ? "Creature" : "Piece")}, {culler.CullDistance}m)");
            }
        }
    }
}
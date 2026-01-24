using HarmonyLib;
using UnityEngine;
using ValheimPerformanceOverhaul;

namespace ValheimPerformanceOverhaul.Pieces
{
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class DecorOptimizer
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;

            // Functionality 2: Decor Collider Optimization
            if (__instance.m_category == Piece.PieceCategory.Misc)
            {
                OptimizeDecor(__instance);
            }
        }

        private static void OptimizeDecor(Piece piece)
        {
            if (piece == null) return;

            // SAFETY: Do not optimize if it has components that imply dynamic behavior
            if (piece.GetComponent<ZSyncTransform>() != null || 
                piece.GetComponent<Character>() != null ||
                piece.GetComponent<BaseAI>() != null)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[DecorOptimizer] Skipping optimization for {piece.name} (detected dynamic component)");
                return;
            }

            // Exclusion list from config
            string cleanName = piece.name.Replace("(Clone)", "");
            if (Plugin.CullerExclusions.Value.Contains(cleanName))
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[DecorOptimizer] Skipping optimization for {piece.name} (manual exclusion)");
                return;
            }

            // We KEEP colliders enabled so things stay interactable (Hover, Strike, etc.)
            // But we simplify physics by making Rigidbodies kinematic below.

            var rigidbodies = piece.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rigidbodies)
            {
                if (rb != null)
                {
                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[DecorOptimizer] Optimizing Rigidbody on {piece.name} (child: {rb.gameObject.name}), isKinematic: {rb.isKinematic}");

                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                }
            }
        }
    }
}

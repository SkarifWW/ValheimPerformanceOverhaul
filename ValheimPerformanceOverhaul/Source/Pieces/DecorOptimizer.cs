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

            GameObject go = piece.gameObject;
            if (go == null) return;

            try
            {
                // ? ИСПРАВЛЕНО: Безопасная работа с Rigidbody
                var rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rigidbodies)
                {
                    if (rb != null)
                    {
                        // ? КРИТИЧНО: Обнуляем velocity ТОЛЬКО если НЕ kinematic
                        if (!rb.isKinematic)
                        {
                            rb.linearVelocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                            rb.Sleep();
                        }

                        // ? Делаем kinematic ПОСЛЕ обнуления velocity
                        rb.isKinematic = true;

                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogInfo($"[DecorOptimizer] Made Rigidbody kinematic on {piece.name} (child: {rb.gameObject.name})");
                    }
                }

                // ? Коллайдеры остаются активными для интерактивности
                // (DistanceCuller отключит их на расстоянии если нужно)
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                {
                    Plugin.Log.LogError($"[DecorOptimizer] Error on {go.name}: {e.Message}");
                }
            }
        }
    }
}
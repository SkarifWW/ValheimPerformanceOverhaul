using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.Pieces
{
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class DecorOptimizer
    {
        // ✅ КРИТИЧНО: Используем WeakReference для автоматической очистки
        private static readonly HashSet<int> _processedInstances = new HashSet<int>(256);

        // ✅ КРИТИЧНО: Счетчик для периодической очистки
        private static int _totalProcessed = 0;
        private const int CLEANUP_THRESHOLD = 200; // Уменьшено с 500 для более частой очистки

        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            // ✅ КРИТИЧНО: Проверяем конфиг ПЕРВЫМ делом
            if (!Plugin.PieceOptimizationEnabled.Value) return;

            if (__instance == null || __instance.gameObject == null) return;
            if (__instance.gameObject.layer == LayerMask.NameToLayer("ghost")) return;

            // ✅ КРИТИЧНО: Только для Misc категории
            if (__instance.m_category != Piece.PieceCategory.Misc) return;

            // ✅ Периодическая очистка
            _totalProcessed++;
            if (_totalProcessed >= CLEANUP_THRESHOLD)
            {
                _totalProcessed = 0;
                _processedInstances.Clear();

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo("[DecorOptimizer] Cleared processed cache");
            }

            int instanceId = __instance.GetInstanceID();

            // ✅ КРИТИЧНО: Защита от повторной обработки
            if (!_processedInstances.Add(instanceId))
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogWarning($"[DecorOptimizer] Skipping duplicate: {__instance.name}");
                return;
            }

            // Исключения
            string name = __instance.name.ToLower();
            if (name.Contains("portal") || name.Contains("bed") || name.Contains("workbench"))
                return;

            GameObject go = __instance.gameObject;
            if (go == null) return;

            try
            {
                // ✅ КРИТИЧНО: НЕ трогаем Rigidbody ВООБЩЕ
                // DistanceCuller управляет физикой

                // Только отключаем коллайдеры для неинтерактивного декора
                bool isInteractive = go.GetComponent<Hoverable>() != null ||
                                     go.GetComponent<Interactable>() != null ||
                                     go.GetComponent<Container>() != null ||
                                     go.GetComponent<Door>() != null;

                if (!isInteractive)
                {
                    var colliders = go.GetComponentsInChildren<Collider>(false);
                    if (colliders != null && colliders.Length > 0)
                    {
                        foreach (var col in colliders)
                        {
                            if (col != null && !col.isTrigger)
                            {
                                col.enabled = false;
                            }
                        }

                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogInfo($"[DecorOptimizer] Disabled {colliders.Length} colliders on {name}");
                    }
                }
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                {
                    Plugin.Log.LogError($"[DecorOptimizer] Error on {go.name}: {e.Message}");
                }
            }
        }

        // ✅ НОВОЕ: Принудительная очистка при выгрузке мира
        [HarmonyPatch(typeof(ZNet), "Shutdown")]
        [HarmonyPostfix]
        private static void OnWorldUnload()
        {
            _processedInstances.Clear();
            _totalProcessed = 0;

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo("[DecorOptimizer] Cleared on world unload");
        }
    }
}
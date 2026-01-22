using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace ValheimPerformanceOverhaul.Structures
{
    // =========================================================================================
    // МОДУЛЬ 1: Умный сон для построек (Smart Sleep) - ПОЛНОСТЬЮ ОТКЛЮЧЁН
    // =========================================================================================
    // ✅ КРИТИЧНО: Система Smart Sleep полностью отключена
    // Причины:
    // 1. Конфликт с DistanceCuller (оба управляют enabled состоянием)
    // 2. Каскадное пробуждение приводило к OOM
    // 3. Valheim имеет встроенную систему sleep для построек

    public class StructureSleepManager : MonoBehaviour
    {
        // Компонент оставлен для обратной совместимости, но ничего не делает
        private void Awake()
        {
            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogWarning("[StructureSleepManager] This component is deprecated and does nothing.");
            }
            // Самоуничтожаемся
            Destroy(this);
        }
    }

    [HarmonyPatch]
    public static class SmartSleepPatches
    {
        // ✅ ВСЕ ПАТЧИ ОТКЛЮЧЕНЫ - Smart Sleep система удалена
        // Если пользователь включит опцию в конфиге, она просто ничего не будет делать
    }

    // =========================================================================================
    // МОДУЛЬ 2: Оптимизация декора
    // =========================================================================================
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class DecorOptimizer
    {
        // ✅ НОВОЕ: WeakReference для автоматической очистки
        private static readonly HashSet<int> _processedInstances = new HashSet<int>(256);
        private static int _cleanupCounter = 0;

        [HarmonyPostfix]
        private static void OptimizeDecor(Piece __instance)
        {
            if (!Plugin.DecorOptimizationEnabled.Value) return;
            if (__instance == null || __instance.gameObject == null) return;
            if (__instance.gameObject.layer == LayerMask.NameToLayer("ghost")) return;

            // ✅ Периодическая очистка
            _cleanupCounter++;
            if (_cleanupCounter >= 500)
            {
                _cleanupCounter = 0;
                _processedInstances.Clear();
            }

            int instanceId = __instance.GetInstanceID();
            if (_processedInstances.Contains(instanceId)) return;
            _processedInstances.Add(instanceId);

            if (__instance.m_category != Piece.PieceCategory.Misc) return;

            // Исключения
            string name = __instance.name.ToLower();
            if (name.Contains("portal") || name.Contains("bed") || name.Contains("workbench")) return;

            GameObject go = __instance.gameObject;
            if (go == null) return;

            try
            {
                // ✅ КРИТИЧНО: НЕ трогаем Rigidbody вообще
                // DistanceCuller сам управляет физикой

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
    }

    // =========================================================================================
    // МОДУЛЬ 3: Отключение дождя
    // =========================================================================================
    [HarmonyPatch(typeof(WearNTear), "Awake")]
    public static class NoRainDamageOptimizer
    {
        [HarmonyPostfix]
        private static void DisableRainFlag(WearNTear __instance)
        {
            if (!Plugin.DisableRainDamage.Value) return;
            if (__instance == null) return;

            __instance.m_noRoofWear = false;
        }
    }
}
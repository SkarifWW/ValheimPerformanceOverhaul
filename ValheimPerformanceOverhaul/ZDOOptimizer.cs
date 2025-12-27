using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.Network
{
    [HarmonyPatch]
    public static class ZDOOptimizationPatches
    {
        private static readonly Dictionary<ZDO, float> _lastSyncTime = new Dictionary<ZDO, float>();
        private static float _syncInterval = 2.0f;

        // ПАТЧ 1: Throttling ZDO синхронизации для статичных объектов
        [HarmonyPatch(typeof(ZDOMan), "CreateSyncList")]
        [HarmonyPrefix]
        private static void ThrottleStaticZDOs(ZDOMan __instance, List<ZDO> ___m_tempToSync)
        {
            if (!Plugin.ZDOOptimizationEnabled.Value) return;

            _syncInterval = Plugin.ZDOSyncInterval.Value;

            if (___m_tempToSync == null) return;

            float currentTime = Time.time;
            List<ZDO> toRemove = new List<ZDO>();

            foreach (var zdo in ___m_tempToSync)
            {
                if (zdo == null) continue;

                // Проверяем, является ли объект статичным (постройка)
                if (IsStaticZDO(zdo))
                {
                    // Проверяем, когда последний раз синхронизировали
                    if (_lastSyncTime.TryGetValue(zdo, out float lastSync))
                    {
                        if (currentTime - lastSync < _syncInterval)
                        {
                            toRemove.Add(zdo);
                            continue;
                        }
                    }

                    _lastSyncTime[zdo] = currentTime;
                }
            }

            // Удаляем из списка синхронизации
            foreach (var zdo in toRemove)
            {
                ___m_tempToSync.Remove(zdo);
            }

            if (Plugin.DebugLoggingEnabled.Value && toRemove.Count > 0)
            {
                Plugin.Log.LogInfo($"[ZDO] Throttled {toRemove.Count} static ZDO syncs");
            }
        }

        private static bool IsStaticZDO(ZDO zdo)
        {
            if (zdo == null) return false;

            // Проверяем тип объекта
            int prefabHash = zdo.GetPrefab();

            // Постройки обычно не имеют velocity
            if (zdo.GetVec3("velocity", Vector3.zero) != Vector3.zero)
            {
                return false; // Движущийся объект
            }

            // Проверяем, есть ли у объекта owner (игрок или AI)
            long owner = zdo.GetOwner();
            if (owner != 0)
            {
                // Есть владелец - вероятно динамический объект (игрок, NPC)
                return false;
            }

            // Вероятно статичный объект (постройка)
            return true;
        }

        // ПАТЧ 2: Уменьшаем частоту Send для далеких ZDO
        [HarmonyPatch(typeof(ZDOMan), "SendZDOs")]
        [HarmonyPrefix]
        private static void OptimizeDistantZDOs(ZDOMan __instance)
        {
            if (!Plugin.ZDOOptimizationEnabled.Value || Player.m_localPlayer == null) return;

            // Этот патч можно расширить для дополнительной оптимизации
            // пока оставим заглушку для будущих улучшений
        }

        // ПАТЧ 3: Очистка кэша
        [HarmonyPatch(typeof(ZDOMan), "Update")]
        [HarmonyPostfix]
        private static void CleanupCache()
        {
            if (!Plugin.ZDOOptimizationEnabled.Value) return;

            // Очищаем кэш раз в минуту
            if (Time.frameCount % 3600 == 0) // 60fps * 60s
            {
                // Удаляем старые записи
                List<ZDO> toRemove = new List<ZDO>();
                float currentTime = Time.time;

                foreach (var kvp in _lastSyncTime)
                {
                    if (kvp.Key == null || !kvp.Key.IsValid() || currentTime - kvp.Value > 60f)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var zdo in toRemove)
                {
                    _lastSyncTime.Remove(zdo);
                }

                if (Plugin.DebugLoggingEnabled.Value && toRemove.Count > 0)
                {
                    Plugin.Log.LogInfo($"[ZDO] Cleaned {toRemove.Count} cached entries");
                }
            }
        }
    }
}
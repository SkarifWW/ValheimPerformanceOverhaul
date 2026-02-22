/*
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.Network
{
    [HarmonyPatch]
    public static class ZDOOptimizationPatches
    {
        private static readonly Dictionary<ZDO, float> _lastSyncTime = new Dictionary<ZDO, float>();
        private static readonly List<ZDO> _reusableRemoveList = new List<ZDO>(128);         private static float _syncInterval = 2.0f;

                private static readonly Dictionary<int, ZDOType> _prefabTypeCache = new Dictionary<int, ZDOType>();

        private enum ZDOType
        {
            Unknown,
            Piece,                  Character,              Item,                   Other               }

                [HarmonyPatch(typeof(ZDOMan), "CreateSyncList")]
        [HarmonyPrefix]
        private static void ThrottleStaticZDOs(ZDOMan __instance, List<ZDO> ___m_tempToSync)
        {
            if (!Plugin.ZDOOptimizationEnabled.Value) return;

            _syncInterval = Plugin.ZDOSyncInterval.Value;

            if (___m_tempToSync == null) return;

            float currentTime = Time.time;
            _reusableRemoveList.Clear(); 
            foreach (var zdo in ___m_tempToSync)
            {
                if (zdo == null) continue;

                                if (IsStaticZDO(zdo))
                {
                                        float interval = _syncInterval;

                    if (Player.m_localPlayer != null)
                    {
                        float dist = Vector3.Distance(zdo.GetPosition(), Player.m_localPlayer.transform.position);
                        if (dist > 60f) interval *= 2f;                          if (dist > 120f) interval *= 4f;                     }

                                        if (_lastSyncTime.TryGetValue(zdo, out float lastSync))
                    {
                        if (currentTime - lastSync < interval)
                        {
                            _reusableRemoveList.Add(zdo);
                            continue;
                        }
                    }

                    _lastSyncTime[zdo] = currentTime;
                }
            }

                        foreach (var zdo in _reusableRemoveList)
            {
                ___m_tempToSync.Remove(zdo);
            }

            if (Plugin.DebugLoggingEnabled.Value && _reusableRemoveList.Count > 0)
            {
                Plugin.Log.LogInfo($"[ZDO] Throttled {_reusableRemoveList.Count} static ZDO syncs");
            }
        }

                private static bool IsStaticZDO(ZDO zdo)
        {
            if (zdo == null) return false;

            int prefabHash = zdo.GetPrefab();

                        if (_prefabTypeCache.TryGetValue(prefabHash, out ZDOType cachedType))
            {
                return cachedType == ZDOType.Piece;
            }

                        ZDOType type = DetermineZDOType(prefabHash);
            _prefabTypeCache[prefabHash] = type;

            return type == ZDOType.Piece;
        }

        private static ZDOType DetermineZDOType(int prefabHash)
        {
            if (ZNetScene.instance == null) return ZDOType.Unknown;

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabHash);
            if (prefab == null) return ZDOType.Unknown;

            
                        if (prefab.GetComponent<Piece>() != null)
            {
                return ZDOType.Piece;
            }

                        if (prefab.GetComponent<Character>() != null)
            {
                return ZDOType.Character;
            }

                        if (prefab.GetComponent<ItemDrop>() != null)
            {
                return ZDOType.Item;
            }

                        return ZDOType.Other;
        }

                private static bool IsItemStatic(ZDO zdo)
        {
                                    if (zdo.GetVec3("velocity", Vector3.zero) != Vector3.zero)
                return false;

                        if (zdo.GetInt("inContainer", 0) != 0)
                return false;

                        if (zdo.GetLong("pickedUp", 0L) != 0L)
                return false;

            return true;
        }

                [HarmonyPatch(typeof(ZDOMan), "Update")]
        [HarmonyPostfix]
        private static void CleanupCache()
        {
            if (!Plugin.ZDOOptimizationEnabled.Value) return;

                        if (Time.frameCount % 3600 == 0)
            {
                _reusableRemoveList.Clear();
                float currentTime = Time.time;

                foreach (var kvp in _lastSyncTime)
                {
                    if (kvp.Key == null || !kvp.Key.IsValid() || currentTime - kvp.Value > 60f)
                    {
                        _reusableRemoveList.Add(kvp.Key);
                    }
                }

                foreach (var zdo in _reusableRemoveList)
                {
                    _lastSyncTime.Remove(zdo);
                }

                                if (_prefabTypeCache.Count > 1000)
                {
                    Plugin.Log.LogWarning($"[ZDO] Prefab cache large ({_prefabTypeCache.Count}), consider clearing");
                }

                if (Plugin.DebugLoggingEnabled.Value && _reusableRemoveList.Count > 0)
                {
                    Plugin.Log.LogInfo($"[ZDO] Cleaned {_reusableRemoveList.Count} cached entries");
                }
            }
        }

                [HarmonyPatch(typeof(ZNet), "Shutdown")]
        [HarmonyPostfix]
        private static void ClearCacheOnShutdown()
        {
            _lastSyncTime.Clear();
            _prefabTypeCache.Clear();
            _reusableRemoveList.Clear();

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo("[ZDO] Cleared all caches on shutdown");
        }

                [HarmonyPatch(typeof(ZDOMan), "Update")]
        [HarmonyPostfix]
        private static void LogStats()
        {
            if (!Plugin.DebugLoggingEnabled.Value || !Plugin.ZDOOptimizationEnabled.Value) return;

                        if (Time.frameCount % 18000 == 0)
            {
                Plugin.Log.LogInfo($"[ZDO] Stats - Sync cache: {_lastSyncTime.Count}, Type cache: {_prefabTypeCache.Count}");
            }
        }
    }
}
*/
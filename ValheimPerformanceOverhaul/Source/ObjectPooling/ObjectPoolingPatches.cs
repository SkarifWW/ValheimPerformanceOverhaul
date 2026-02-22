using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace ValheimPerformanceOverhaul.ObjectPooling
{
    [HarmonyPatch]
    public static class ObjectPoolingPatches
    {
        private static readonly FieldInfo _zdoField = AccessTools.Field(typeof(ZNetView), "m_zdo");
        private static readonly FieldInfo _instancesField = AccessTools.Field(typeof(ZNetScene), "m_instances");

                private static readonly HashSet<string> _poolingExclusions = new HashSet<string>
        {
                        "spear_bronze", "spear_chitin", "spear_flint", "spear_carapace",
            
                        "arrow_wood", "arrow_fire", "arrow_flint", "arrow_bronze",
            "arrow_iron", "arrow_silver", "arrow_poison", "arrow_frost",
            "arrow_needle", "arrow_obsidian", "arrow_carapace",
            
                        "bomb", "tankard", "tankard_odin",
            
                        "drop_stone", "drop_wood"         };

        private static bool CanPoolObject(GameObject obj)
        {
            if (obj == null) return false;

            string prefabName = obj.name.Replace("(Clone)", "").ToLower();
            if (_poolingExclusions.Contains(prefabName))
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Excluded from pooling: {prefabName}");
                return false;
            }

            var itemDrop = obj.GetComponent<ItemDrop>();
            if (itemDrop != null && itemDrop.m_itemData != null)
            {
                var itemData = itemDrop.m_itemData;

                                if (itemData.m_shared != null &&
                    itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon &&
                    itemData.m_shared.m_attack != null &&
                    itemData.m_shared.m_attack.m_attackProjectile != null)
                {
                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[ObjectPooling] Excluded projectile weapon: {prefabName}");
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPrefix]
        private static bool CreateObjectPrefix(ZNetScene __instance, ZDO zdo, ref GameObject __result)
        {
            if (!Plugin.ObjectPoolingEnabled.Value) return true;

            GameObject prefab = __instance.GetPrefab(zdo.GetPrefab());
            if (prefab == null) return true;

                        if (prefab.GetComponent<ItemDrop>() == null) return true;

                        if (!CanPoolObject(prefab)) return true;

                        if (ObjectPoolManager.TryGetObject(prefab.name, out GameObject obj))
            {
                if (obj == null)
                {
                    Plugin.Log.LogWarning($"[ObjectPooling] Pool returned null for {prefab.name}, using original spawn");
                    return true;
                }

                                if (obj.activeSelf)
                {
                    Plugin.Log.LogError($"[ObjectPooling] Pool returned active object for {prefab.name}! Using original spawn");
                    return true;
                }

                var netView = obj.GetComponent<ZNetView>();
                if (netView != null)
                {
                    var existingZDO = netView.GetZDO();
                    if (existingZDO != null && existingZDO.IsValid())
                    {
                        Plugin.Log.LogError($"[ObjectPooling] Pool returned object with valid ZDO for {prefab.name}! Using original spawn");
                        return true;
                    }
                }

                                try
                {
                    obj.transform.position = zdo.GetPosition();
                    obj.transform.rotation = zdo.GetRotation();
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"[ObjectPooling] Error setting transform: {e.Message}");
                    return true;
                }

                                obj.SetActive(true);

                                var pooledObj = obj.GetComponent<PooledObject>();
                if (pooledObj == null)
                {
                    pooledObj = obj.AddComponent<PooledObject>();
                }
                pooledObj.Initialize(prefab.name);

                                if (netView != null)
                {
                    try
                    {
                                                var oldZDO = netView.GetZDO();
                        if (oldZDO != null && oldZDO.IsValid())
                        {
                            netView.ResetZDO();
                        }

                                                if (_zdoField != null)
                        {
                            _zdoField.SetValue(netView, zdo);
                        }

                                                if (_instancesField != null)
                        {
                            var instances = (Dictionary<ZDO, ZNetView>)_instancesField.GetValue(__instance);
                            if (instances != null)
                            {
                                                                if (instances.ContainsKey(zdo))
                                {
                                    Plugin.Log.LogError($"[ObjectPooling] ZDO already exists in instances! {prefab.name}");
                                    obj.SetActive(false);
                                    ObjectPoolManager.ReturnObject(obj);
                                    return true;
                                }

                                instances[zdo] = netView;
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Plugin.Log.LogError($"[ObjectPooling] Error setting up ZNetView: {e.Message}");
                        obj.SetActive(false);
                        ObjectPoolManager.ReturnObject(obj);
                        return true;
                    }
                }

                __result = obj;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Reused object: {prefab.name}");

                return false;             }

                        return true;
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
        [HarmonyPrefix]
        private static bool DestroyPrefix(ZNetScene __instance, GameObject go)
        {
            if (!Plugin.ObjectPoolingEnabled.Value || go == null) return true;

                        if (go.GetComponent<ItemDrop>() == null)
            {
                return true;
            }

                        if (!CanPoolObject(go)) return true;

            var netView = go.GetComponent<ZNetView>();
            if (netView == null) return true;

                        try
            {
                if (_instancesField != null)
                {
                    var instances = (Dictionary<ZDO, ZNetView>)_instancesField.GetValue(__instance);
                    var zdo = netView.GetZDO();

                    if (instances != null && zdo != null)
                    {
                        instances.Remove(zdo);

                                                if (zdo.IsOwner() && ZDOMan.instance != null)
                        {
                            ZDOMan.instance.DestroyZDO(zdo);
                        }
                    }

                                        if (netView.GetZDO() != null)
                    {
                        netView.ResetZDO();
                    }
                }

                                ObjectPoolManager.ReturnObject(go);

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Returned to pool: {go.name}");

                return false;             }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ObjectPooling] Error returning to pool: {e.Message}");
                return true;             }
        }

                [HarmonyPatch(typeof(ZNetScene), "Update")]
        [HarmonyPostfix]
        private static void PerformPeriodicMaintenance()
        {
            if (!Plugin.ObjectPoolingEnabled.Value) return;

                        if (Time.frameCount % 3600 == 0)
            {
                ObjectPoolManager.PerformMaintenance();
            }

                        if (Plugin.DebugLoggingEnabled.Value && Time.frameCount % 18000 == 0)
            {
                ObjectPoolManager.LogPoolStats();
            }
        }

                [HarmonyPatch(typeof(ZNet), "Shutdown")]
        [HarmonyPostfix]
        private static void ClearPoolOnShutdown()
        {
            if (!Plugin.ObjectPoolingEnabled.Value) return;
            ObjectPoolManager.Clear();
        }
    }
}
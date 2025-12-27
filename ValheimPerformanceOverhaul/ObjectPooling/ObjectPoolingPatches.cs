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

        // НОВОЕ: Список исключений для пулинга
        private static readonly HashSet<string> _poolingExclusions = new HashSet<string>
        {
            // Метательное оружие (может дюпаться)
            "spear_bronze", "spear_chitin", "spear_flint", "spear_carapace",
            
            // Стрелы (могут застревать в геометрии)
            "arrow_wood", "arrow_fire", "arrow_flint", "arrow_bronze",
            "arrow_iron", "arrow_silver", "arrow_poison", "arrow_frost",
            "arrow_needle", "arrow_obsidian", "arrow_carapace",
            
            // Бомбы и метаемые предметы
            "bomb", "tankard", "tankard_odin"
        };

        // НОВЫЙ МЕТОД: Проверка, можно ли пулить объект
        private static bool CanPoolObject(GameObject obj)
        {
            if (obj == null) return false;

            // Проверяем по имени префаба
            string prefabName = obj.name.Replace("(Clone)", "").ToLower();
            if (_poolingExclusions.Contains(prefabName))
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Excluded from pooling: {prefabName}");
                return false;
            }

            // Дополнительная проверка: есть ли компонент, указывающий на метательное оружие
            var itemDrop = obj.GetComponent<ItemDrop>();
            if (itemDrop != null && itemDrop.m_itemData != null)
            {
                var itemData = itemDrop.m_itemData;

                // Проверяем, является ли предмет оружием с возможностью метания
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

            // Пулим только ItemDrop
            if (prefab.GetComponent<ItemDrop>() == null) return true;

            // ИЗМЕНЕНО: Проверяем исключения
            if (!CanPoolObject(prefab)) return true;

            if (ObjectPoolManager.TryGetObject(prefab.name, out GameObject obj))
            {
                if (obj == null) return true;

                obj.transform.position = zdo.GetPosition();
                obj.transform.rotation = zdo.GetRotation();
                obj.SetActive(true);

                var pooledObj = obj.GetComponent<PooledObject>();
                if (pooledObj == null)
                {
                    pooledObj = obj.AddComponent<PooledObject>();
                    pooledObj.Initialize(prefab.name);
                }

                var netView = obj.GetComponent<ZNetView>();
                if (netView)
                {
                    try
                    {
                        if (netView.GetZDO() != null)
                            netView.ResetZDO();
                    }
                    catch { }

                    if (_zdoField != null)
                    {
                        _zdoField.SetValue(netView, zdo);
                    }

                    if (_instancesField != null)
                    {
                        var instances = (Dictionary<ZDO, ZNetView>)_instancesField.GetValue(__instance);
                        if (instances != null)
                        {
                            instances[zdo] = netView;
                        }
                    }
                }

                __result = obj;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
        [HarmonyPrefix]
        private static bool DestroyPrefix(ZNetScene __instance, GameObject go)
        {
            if (!Plugin.ObjectPoolingEnabled.Value || go == null) return true;

            // Проверяем ItemDrop
            if (go.GetComponent<ItemDrop>() == null)
            {
                return true;
            }

            // ИЗМЕНЕНО: Проверяем исключения
            if (!CanPoolObject(go)) return true;

            var netView = go.GetComponent<ZNetView>();
            if (netView == null) return true;

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
                    try
                    {
                        netView.ResetZDO();
                    }
                    catch { }
                }
            }

            ObjectPoolManager.ReturnObject(go);
            return false;
        }
    }
}
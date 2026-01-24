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

        // Список исключений для пулинга
        private static readonly HashSet<string> _poolingExclusions = new HashSet<string>
        {
            // Метательное оружие
            "spear_bronze", "spear_chitin", "spear_flint", "spear_carapace",
            
            // Стрелы
            "arrow_wood", "arrow_fire", "arrow_flint", "arrow_bronze",
            "arrow_iron", "arrow_silver", "arrow_poison", "arrow_frost",
            "arrow_needle", "arrow_obsidian", "arrow_carapace",
            
            // Бомбы и метаемые предметы
            "bomb", "tankard", "tankard_odin",
            
            // ✅ НОВОЕ: Добавляем проблемные предметы
            "drop_stone", "drop_wood" // Могут дюпаться при добыче
        };

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

                // Проверяем метательное оружие
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

            // Проверяем исключения
            if (!CanPoolObject(prefab)) return true;

            // ✅ ИСПРАВЛЕНО: Безопасное получение объекта из пула
            if (ObjectPoolManager.TryGetObject(prefab.name, out GameObject obj))
            {
                if (obj == null)
                {
                    Plugin.Log.LogWarning($"[ObjectPooling] Pool returned null for {prefab.name}, using original spawn");
                    return true;
                }

                // ✅ КРИТИЧНО: Дополнительные проверки безопасности
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

                // ✅ Безопасная установка позиции и ротации
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

                // ✅ Активируем объект ПЕРЕД установкой компонентов
                obj.SetActive(true);

                // ✅ Инициализируем PooledObject
                var pooledObj = obj.GetComponent<PooledObject>();
                if (pooledObj == null)
                {
                    pooledObj = obj.AddComponent<PooledObject>();
                }
                pooledObj.Initialize(prefab.name);

                // ✅ ИСПРАВЛЕНО: Безопасная установка ZDO
                if (netView != null)
                {
                    try
                    {
                        // Сбрасываем старый ZDO если есть
                        var oldZDO = netView.GetZDO();
                        if (oldZDO != null && oldZDO.IsValid())
                        {
                            netView.ResetZDO();
                        }

                        // Устанавливаем новый ZDO через рефлексию
                        if (_zdoField != null)
                        {
                            _zdoField.SetValue(netView, zdo);
                        }

                        // Регистрируем в instances
                        if (_instancesField != null)
                        {
                            var instances = (Dictionary<ZDO, ZNetView>)_instancesField.GetValue(__instance);
                            if (instances != null)
                            {
                                // ✅ КРИТИЧНО: Проверяем, нет ли уже такого ZDO
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

                return false; // Блокируем оригинальный spawn
            }

            // Пул пуст - используем оригинальный spawn
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

            // Проверяем исключения
            if (!CanPoolObject(go)) return true;

            var netView = go.GetComponent<ZNetView>();
            if (netView == null) return true;

            // ✅ ИСПРАВЛЕНО: Безопасная очистка ZDO
            try
            {
                if (_instancesField != null)
                {
                    var instances = (Dictionary<ZDO, ZNetView>)_instancesField.GetValue(__instance);
                    var zdo = netView.GetZDO();

                    if (instances != null && zdo != null)
                    {
                        instances.Remove(zdo);

                        // ✅ Уничтожаем ZDO только если мы владелец
                        if (zdo.IsOwner() && ZDOMan.instance != null)
                        {
                            ZDOMan.instance.DestroyZDO(zdo);
                        }
                    }

                    // ✅ Сбрасываем ZDO reference
                    if (netView.GetZDO() != null)
                    {
                        netView.ResetZDO();
                    }
                }

                // ✅ Возвращаем в пул
                ObjectPoolManager.ReturnObject(go);

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Returned to pool: {go.name}");

                return false; // Блокируем оригинальное уничтожение
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ObjectPooling] Error returning to pool: {e.Message}");
                return true; // Fallback на оригинальное уничтожение
            }
        }

        // ✅ НОВОЕ: Периодическое обслуживание пула
        [HarmonyPatch(typeof(ZNetScene), "Update")]
        [HarmonyPostfix]
        private static void PerformPeriodicMaintenance()
        {
            if (!Plugin.ObjectPoolingEnabled.Value) return;

            // Раз в минуту (60fps * 60s = 3600 frames)
            if (Time.frameCount % 3600 == 0)
            {
                ObjectPoolManager.PerformMaintenance();
            }

            // Логируем статистику раз в 5 минут
            if (Plugin.DebugLoggingEnabled.Value && Time.frameCount % 18000 == 0)
            {
                ObjectPoolManager.LogPoolStats();
            }
        }

        // ✅ НОВОЕ: Очистка пула при смене мира
        [HarmonyPatch(typeof(ZNet), "Shutdown")]
        [HarmonyPostfix]
        private static void ClearPoolOnShutdown()
        {
            if (!Plugin.ObjectPoolingEnabled.Value) return;
            ObjectPoolManager.Clear();
        }
    }
}
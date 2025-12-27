using UnityEngine;
using System.Collections.Generic;
using HarmonyLib;

namespace ValheimPerformanceOverhaul.ObjectPooling
{
    public static class ObjectPoolManager
    {
        private static readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>();
        private static Transform _poolParent;
        private const int MAX_POOL_SIZE = 100;

        public static void Initialize()
        {
            if (_poolParent != null) return;

            var poolObject = new GameObject("_VPO_ObjectPool");
            _poolParent = poolObject.transform;
            Object.DontDestroyOnLoad(poolObject);
            _pools.Clear();

            Plugin.Log.LogInfo("[ObjectPooling] Manager initialized.");
        }

        public static bool TryGetObject(string prefabName, out GameObject instance)
        {
            int prefabHash = prefabName.GetStableHashCode();
            instance = null;

            if (_pools.TryGetValue(prefabHash, out Queue<GameObject> pool) && pool.Count > 0)
            {
                instance = pool.Dequeue();
                if (instance == null)
                {
                    return TryGetObject(prefabName, out instance);
                }

                // ДОБАВЛЕНО: Сброс состояния перед выдачей из пула
                ResetObjectState(instance);
                return true;
            }
            return false;
        }

        public static void ReturnObject(GameObject instance)
        {
            if (instance == null) return;

            // ДОБАВЛЕНО: Полная очистка состояния перед возвратом в пул
            CleanupObjectState(instance);

            instance.transform.SetParent(null);
            instance.SetActive(false);
            instance.transform.SetParent(_poolParent);

            var pooledObj = instance.GetComponent<PooledObject>();
            int prefabHash;

            if (pooledObj != null)
            {
                prefabHash = pooledObj.PrefabHash;
            }
            else
            {
                string prefabName = instance.name.Replace("(Clone)", "");
                prefabHash = prefabName.GetStableHashCode();
            }

            if (!_pools.TryGetValue(prefabHash, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                _pools.Add(prefabHash, pool);
            }

            if (pool.Count < MAX_POOL_SIZE)
            {
                pool.Enqueue(instance);
            }
            else
            {
                Object.Destroy(instance);

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogWarning($"[ObjectPooling] Pool full ({MAX_POOL_SIZE}). Destroying excess object.");
            }
        }

        // НОВЫЙ МЕТОД: Очистка состояния при возврате в пул
        private static void CleanupObjectState(GameObject instance)
        {
            try
            {
                // 1. Сброс физики
                var rb = instance.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep(); // Усыпляем физику
                }

                // 2. Очистка ItemDrop (без рефлексии - безопасно)
                var itemDrop = instance.GetComponent<ItemDrop>();
                if (itemDrop != null)
                {
                    // Отключаем коллизию на время нахождения в пуле
                    var collider = instance.GetComponent<Collider>();
                    if (collider != null)
                        collider.enabled = false;
                }

                // 3. Очистка партикл-эффектов
                var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }

                // 4. Сброс позиции в безопасное место
                instance.transform.position = new Vector3(0, -10000, 0);
                instance.transform.rotation = Quaternion.identity;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Cleaned up state for {instance.name}");
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogError($"[ObjectPooling] Error cleaning up object state: {e.Message}");
            }
        }

        // НОВЫЙ МЕТОД: Восстановление состояния при выдаче из пула
        private static void ResetObjectState(GameObject instance)
        {
            try
            {
                // 1. Пробуждаем физику
                var rb = instance.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.WakeUp();
                }

                // 2. Включаем коллайдер обратно
                var collider = instance.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = true;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Reset state for {instance.name}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ObjectPooling] Error resetting object state: {e.Message}");
            }
        }
    }
}
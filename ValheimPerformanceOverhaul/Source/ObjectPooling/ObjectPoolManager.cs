using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.ObjectPooling
{
    public static class ObjectPoolManager
    {
        private static readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>();
        private static readonly HashSet<GameObject> _objectsInUse = new HashSet<GameObject>();
        private static Transform _poolParent;
        private const int MAX_POOL_SIZE = 100;
        private static readonly object _lockObject = new object();

        // ✅ НОВОЕ: Reusable lists для уменьшения GC
        private static readonly List<GameObject> _tempActiveCheck = new List<GameObject>(16);

        public static void Initialize()
        {
            if (_poolParent != null) return;

            var poolObject = new GameObject("_VPO_ObjectPool");
            _poolParent = poolObject.transform;
            Object.DontDestroyOnLoad(poolObject);

            lock (_lockObject)
            {
                _pools.Clear();
                _objectsInUse.Clear();
            }

            Plugin.Log.LogInfo("[ObjectPooling] Manager initialized.");
        }

        public static bool TryGetObject(string prefabName, out GameObject instance)
        {
            int prefabHash = prefabName.GetStableHashCode();
            instance = null;

            lock (_lockObject)
            {
                if (_pools.TryGetValue(prefabHash, out Queue<GameObject> pool) && pool.Count > 0)
                {
                    // ✅ ИСПРАВЛЕНО: Безопасное извлечение с проверкой состояния
                    int attempts = 0;
                    while (pool.Count > 0 && attempts < 5)
                    {
                        instance = pool.Dequeue();
                        attempts++;

                        if (instance == null)
                        {
                            Plugin.Log.LogWarning($"[ObjectPooling] Null object in pool for {prefabName}");
                            continue;
                        }

                        // ✅ КРИТИЧНО: Проверяем, не активен ли объект
                        if (instance.activeSelf)
                        {
                            Plugin.Log.LogError($"[ObjectPooling] Object already active: {prefabName}");
                            _objectsInUse.Remove(instance); // Очищаем некорректное состояние
                            continue;
                        }

                        // ✅ КРИТИЧНО: Проверяем ZNetView
                        var netView = instance.GetComponent<ZNetView>();
                        if (netView != null)
                        {
                            var existingZDO = netView.GetZDO();
                            if (existingZDO != null && existingZDO.IsValid())
                            {
                                Plugin.Log.LogError($"[ObjectPooling] Object has valid ZDO: {prefabName}");
                                continue;
                            }
                        }

                        // ✅ КРИТИЧНО: Проверяем, не используется ли уже объект
                        if (_objectsInUse.Contains(instance))
                        {
                            Plugin.Log.LogError($"[ObjectPooling] Object already in use: {prefabName}");
                            continue;
                        }

                        // Объект прошел все проверки - используем его
                        _objectsInUse.Add(instance);
                        ResetObjectState(instance);
                        return true;
                    }

                    // Не нашли подходящий объект после 5 попыток
                    instance = null;
                    return false;
                }
            }

            return false;
        }

        public static void ReturnObject(GameObject instance)
        {
            if (instance == null) return;

            lock (_lockObject)
            {
                // ✅ ИСПРАВЛЕНО: Проверяем, был ли объект выдан из пула
                if (!_objectsInUse.Contains(instance))
                {
                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[ObjectPooling] Object not from pool, destroying: {instance.name}");

                    Object.Destroy(instance);
                    return;
                }

                // Убираем из списка активных
                _objectsInUse.Remove(instance);

                // Полная очистка состояния
                CleanupObjectState(instance);

                instance.transform.SetParent(null);
                instance.SetActive(false);
                instance.transform.SetParent(_poolParent);

                var pooledObj = instance.GetComponent<PooledObject>();
                int prefabHash;

                if (pooledObj != null)
                {
                    prefabHash = pooledObj.PrefabHash;
                    pooledObj.MarkAsAvailable();
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
                        Plugin.Log.LogWarning($"[ObjectPooling] Pool full ({MAX_POOL_SIZE}), destroying: {instance.name}");
                }
            }
        }

        private static void CleanupObjectState(GameObject instance)
        {
            try
            {
                // 1. ✅ ИСПРАВЛЕНО: Безопасный сброс физики
                var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rigidbodies)
                {
                    if (rb != null && !rb.isKinematic) // ✅ Проверяем ПЕРЕД установкой velocity
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.Sleep();
                    }
                }

                // 2. Очистка ItemDrop
                var itemDrop = instance.GetComponent<ItemDrop>();
                if (itemDrop != null)
                {
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

                // 4. Очистка аудио
                var audioSources = instance.GetComponentsInChildren<AudioSource>(true);
                foreach (var audio in audioSources)
                {
                    if (audio != null)
                    {
                        audio.Stop();
                        audio.clip = null; // ✅ Освобождаем ссылку на клип
                    }
                }

                // 5. Сброс позиции
                instance.transform.position = new Vector3(0, -10000, 0);
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one; // ✅ Сброс scale

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Cleaned up: {instance.name}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ObjectPooling] Error cleaning up {instance.name}: {e.Message}");
            }
        }

        private static void ResetObjectState(GameObject instance)
        {
            try
            {
                // 1. ✅ ИСПРАВЛЕНО: Безопасное пробуждение физики
                var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rigidbodies)
                {
                    if (rb != null && !rb.isKinematic) // ✅ Только для non-kinematic
                    {
                        rb.WakeUp();
                    }
                }

                // 2. Включаем коллайдер
                var collider = instance.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = true;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Reset state: {instance.name}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ObjectPooling] Error resetting {instance.name}: {e.Message}");
            }
        }

        // ✅ НОВОЕ: Периодическая очистка мертвых ссылок
        public static void PerformMaintenance()
        {
            lock (_lockObject)
            {
                _tempActiveCheck.Clear();

                // Проверяем активные объекты
                foreach (var obj in _objectsInUse)
                {
                    if (obj == null || !obj.activeSelf)
                    {
                        _tempActiveCheck.Add(obj);
                    }
                }

                // Удаляем мертвые ссылки
                foreach (var obj in _tempActiveCheck)
                {
                    _objectsInUse.Remove(obj);
                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogWarning($"[ObjectPooling] Cleaned dead reference from _objectsInUse");
                }

                // Очищаем мертвые объекты из пулов
                var deadPools = new List<int>();
                foreach (var kvp in _pools)
                {
                    var pool = kvp.Value;
                    var validObjects = new Queue<GameObject>();

                    while (pool.Count > 0)
                    {
                        var obj = pool.Dequeue();
                        if (obj != null)
                        {
                            validObjects.Enqueue(obj);
                        }
                    }

                    if (validObjects.Count == 0)
                    {
                        deadPools.Add(kvp.Key);
                    }
                    else
                    {
                        _pools[kvp.Key] = validObjects;
                    }
                }

                // Удаляем пустые пулы
                foreach (var hash in deadPools)
                {
                    _pools.Remove(hash);
                }

                if (Plugin.DebugLoggingEnabled.Value && (_tempActiveCheck.Count > 0 || deadPools.Count > 0))
                {
                    Plugin.Log.LogInfo($"[ObjectPooling] Maintenance: Removed {_tempActiveCheck.Count} dead references, {deadPools.Count} empty pools");
                }
            }
        }

        public static void LogPoolStats()
        {
            if (!Plugin.DebugLoggingEnabled.Value) return;

            lock (_lockObject)
            {
                int totalPooled = 0;
                foreach (var pool in _pools.Values)
                {
                    totalPooled += pool.Count;
                }

                Plugin.Log.LogInfo($"[ObjectPooling] Stats - In use: {_objectsInUse.Count}, Pooled: {totalPooled}, Pool types: {_pools.Count}");
            }
        }

        // ✅ НОВОЕ: Очистка при выгрузке мира
        public static void Clear()
        {
            lock (_lockObject)
            {
                // Уничтожаем все объекты в пулах
                foreach (var pool in _pools.Values)
                {
                    while (pool.Count > 0)
                    {
                        var obj = pool.Dequeue();
                        if (obj != null)
                            Object.Destroy(obj);
                    }
                }

                _pools.Clear();
                _objectsInUse.Clear();

                Plugin.Log.LogInfo("[ObjectPooling] Cleared all pools");
            }
        }
    }
}
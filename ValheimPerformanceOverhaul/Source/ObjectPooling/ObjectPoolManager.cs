using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.ObjectPooling
{
    public static class ObjectPoolManager
    {
        private static readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>();
        private static readonly HashSet<GameObject> _objectsInUse = new HashSet<GameObject>(); // ✅ НОВОЕ: Трекинг активных объектов
        private static Transform _poolParent;
        private const int MAX_POOL_SIZE = 100;
        private static readonly object _lockObject = new object(); // ✅ НОВОЕ: Thread safety

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

            lock (_lockObject) // ✅ НОВОЕ: Thread safety
            {
                if (_pools.TryGetValue(prefabHash, out Queue<GameObject> pool) && pool.Count > 0)
                {
                    instance = pool.Dequeue();

                    // ✅ НОВОЕ: Проверка на null и повторное использование
                    if (instance == null)
                    {
                        return TryGetObject(prefabName, out instance);
                    }

                    // ✅ КРИТИЧНО: Проверяем, не используется ли уже объект
                    if (_objectsInUse.Contains(instance))
                    {
                        Plugin.Log.LogError($"[ObjectPooling] CRITICAL: Object already in use! {prefabName}");
                        return TryGetObject(prefabName, out instance); // Берем следующий
                    }

                    // Отмечаем как используемый
                    _objectsInUse.Add(instance);

                    // Сброс состояния
                    ResetObjectState(instance);
                    return true;
                }
            }

            return false;
        }

        public static void ReturnObject(GameObject instance)
        {
            if (instance == null) return;

            lock (_lockObject) // ✅ НОВОЕ: Thread safety
            {
                // ✅ НОВОЕ: Проверяем, был ли объект выдан из пула
                if (!_objectsInUse.Contains(instance))
                {
                    // Объект не из пула - просто уничтожаем
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
                    pooledObj.MarkAsAvailable(); // ✅ НОВОЕ: Отмечаем как доступный
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
        }

        private static void CleanupObjectState(GameObject instance)
        {
            try
            {
                // 1. Сброс физики
                var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rigidbodies)
                {
                    if (rb != null)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogInfo($"[ObjectPooling] Cleanup Rigidbody on {instance.name} (child: {rb.gameObject.name}), isKinematic: {rb.isKinematic}, velocity: {rb.velocity}");

                        if (!rb.isKinematic)
                        {
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                            rb.Sleep();
                        }
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

                // 4. ✅ НОВОЕ: Очистка аудио
                var audioSources = instance.GetComponentsInChildren<AudioSource>(true);
                foreach (var audio in audioSources)
                {
                    if (audio != null)
                    {
                        audio.Stop();
                    }
                }

                // 5. Сброс позиции
                instance.transform.position = new Vector3(0, -10000, 0);
                instance.transform.rotation = Quaternion.identity;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Cleaned up state for {instance.name}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ObjectPooling] Error cleaning up object: {e.Message}");
            }
        }

        private static void ResetObjectState(GameObject instance)
        {
            try
            {
                // 1. Пробуждаем физику
                var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rigidbodies)
                {
                    if (rb != null)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogInfo($"[ObjectPooling] Reset Rigidbody on {instance.name} (child: {rb.gameObject.name}), isKinematic: {rb.isKinematic}");

                        if (!rb.isKinematic)
                        {
                            rb.WakeUp();
                        }
                    }
                }

                // 2. Включаем коллайдер
                var collider = instance.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = true;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ObjectPooling] Reset state for {instance.name}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ObjectPooling] Error resetting object: {e.Message}");
            }
        }

        // ✅ НОВОЕ: Метод для прогрева пула
        public static void WarmupPool(string prefabName, int count)
        {
            // Эта функция будет реализована при создании объектов
            Plugin.Log.LogInfo($"[ObjectPooling] Warmup requested for {prefabName} (x{count})");
        }

        // ✅ НОВОЕ: Статистика для дебага
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

                Plugin.Log.LogInfo($"[ObjectPooling] Stats - In use: {_objectsInUse.Count}, Pooled: {totalPooled}");
            }
        }
    }
}
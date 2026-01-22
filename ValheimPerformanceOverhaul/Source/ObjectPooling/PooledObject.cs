using UnityEngine;

namespace ValheimPerformanceOverhaul.ObjectPooling
{
    public class PooledObject : MonoBehaviour
    {
        public int PrefabHash { get; private set; }

        // ✅ КРИТИЧНО: Добавлена защита от повторного использования
        private bool _isInUse = false;
        private int _useCount = 0; // Для дебага

        public void Initialize(string prefabName)
        {
            // ✅ КРИТИЧНО: Проверяем, не используется ли уже объект
            if (_isInUse)
            {
                Plugin.Log.LogError($"[PooledObject] CRITICAL: Object already in use! {prefabName} (Use count: {_useCount})");

                // Форсированный сброс (emergency fallback)
                MarkAsAvailable();
            }

            _isInUse = true;
            _useCount++;
            PrefabHash = prefabName.GetStableHashCode();

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[PooledObject] Initialized: {prefabName} (Use #{_useCount})");
            }
        }

        /// <summary>
        /// ✅ НОВЫЙ МЕТОД: Отмечаем объект как доступный для повторного использования
        /// </summary>
        public void MarkAsAvailable()
        {
            if (!_isInUse && _useCount > 0)
            {
                Plugin.Log.LogWarning($"[PooledObject] Object already available! Double return detected.");
            }

            _isInUse = false;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[PooledObject] Marked as available (Total uses: {_useCount})");
            }
        }

        /// <summary>
        /// ✅ НОВОЕ: Проверка, используется ли объект
        /// </summary>
        public bool IsInUse => _isInUse;

        /// <summary>
        /// ✅ НОВОЕ: Для дебага - сколько раз объект переиспользован
        /// </summary>
        public int UseCount => _useCount;

        private void OnDestroy()
        {
            if (_isInUse)
            {
                Plugin.Log.LogWarning($"[PooledObject] Object destroyed while in use! Use count: {_useCount}");
            }
        }

        // ✅ НОВОЕ: Дополнительная защита - проверка при активации
        private void OnEnable()
        {
            if (!_isInUse && _useCount > 0)
            {
                Plugin.Log.LogError($"[PooledObject] CRITICAL: Object enabled but not marked as in use!");
            }
        }
    }
}
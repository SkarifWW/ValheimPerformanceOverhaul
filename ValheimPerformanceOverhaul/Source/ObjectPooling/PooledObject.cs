using UnityEngine;

namespace ValheimPerformanceOverhaul.ObjectPooling
{
    public class PooledObject : MonoBehaviour
    {
        public int PrefabHash { get; private set; }

                private bool _isInUse = false;
        private int _useCount = 0; 
        public void Initialize(string prefabName)
        {
                        if (_isInUse)
            {
                Plugin.Log.LogError($"[PooledObject] CRITICAL: Object already in use! {prefabName} (Use count: {_useCount})");

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

                                public bool IsInUse => _isInUse;

                                public int UseCount => _useCount;

        private void OnDestroy()
        {
            if (_isInUse)
            {
                Plugin.Log.LogWarning($"[PooledObject] Object destroyed while in use! Use count: {_useCount}");
            }
        }

                private void OnEnable()
        {
            if (!_isInUse && _useCount > 0)
            {
                Plugin.Log.LogError($"[PooledObject] CRITICAL: Object enabled but not marked as in use!");
            }
        }
    }
}
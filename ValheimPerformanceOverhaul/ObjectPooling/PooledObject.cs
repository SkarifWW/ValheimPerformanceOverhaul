using UnityEngine;

namespace ValheimPerformanceOverhaul.ObjectPooling
{
    public class PooledObject : MonoBehaviour
    {
        public int PrefabHash { get; private set; }

        public void Initialize(string prefabName)
        {
            PrefabHash = prefabName.GetStableHashCode();
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ValheimPerformanceOverhaul;

namespace ValheimPerformanceOverhaul.Pieces
{
    public class AsyncWearManager : MonoBehaviour
    {
        public static AsyncWearManager Instance { get; private set; }
        private readonly Queue<WearNTear> _pendingEnable = new Queue<WearNTear>();
        private const int MAX_ENABLES_PER_FRAME = 20; // Configurable?

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Enqueue(WearNTear wnt)
        {
            _pendingEnable.Enqueue(wnt);
        }

        private void Update()
        {
            int count = 0;
            while (_pendingEnable.Count > 0 && count < MAX_ENABLES_PER_FRAME)
            {
                var wnt = _pendingEnable.Dequeue();
                if (wnt != null)
                {
                    wnt.enabled = true;
                }
                count++;
            }
        }
    }

    [HarmonyPatch(typeof(WearNTear), "Awake")]
    public static class WearNTear_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(WearNTear __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;
            
            // Functionality 6: Async Initialization
            // Disable initially, let manager enable it
            // Only if we have a manager
            if (AsyncWearManager.Instance == null)
            {
                var go = new GameObject("_VPO_AsyncWearManager");
                go.AddComponent<AsyncWearManager>();
                // Instance set in Awake
            }

            // Only defer if it's actually enabled
            if (__instance.enabled)
            {
                __instance.enabled = false;
                AsyncWearManager.Instance?.Enqueue(__instance);
            }
        }
    }
}

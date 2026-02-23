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
        private const int MAX_ENABLES_PER_FRAME = 20;

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
                                                if (_pendingEnable.Count == 0) return;

            int count = 0;
            while (_pendingEnable.Count > 0 && count < MAX_ENABLES_PER_FRAME)
            {
                var wnt = _pendingEnable.Dequeue();
                if (wnt != null)
                    wnt.enabled = true;
                count++;
            }
        }

        private void OnDestroy()
        {
            _pendingEnable.Clear();
            Instance = null;
        }
    }

    [HarmonyPatch(typeof(WearNTear), "Awake")]
    public static class WearNTear_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(WearNTear __instance)
        {
            if (!Plugin.PieceOptimizationEnabled.Value) return;

            if (AsyncWearManager.Instance == null)
            {
                var go = new GameObject("_VPO_AsyncWearManager");
                go.AddComponent<AsyncWearManager>();
            }

            if (__instance.enabled)
            {
                __instance.enabled = false;
                AsyncWearManager.Instance?.Enqueue(__instance);
            }
        }
    }
}
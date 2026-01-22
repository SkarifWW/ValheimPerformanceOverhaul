using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.LightCulling
{
    [HarmonyPatch]
    public static class LightLODPatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void InitializeLightLOD(Player __instance)
        {
            if (!Plugin.LightLODEnabled.Value || __instance != Player.m_localPlayer)
            {
                return;
            }

            if (LightLODManager.Instance != null) return;

            var managerObject = new GameObject("_VPO_LightLODManager");
            Object.DontDestroyOnLoad(managerObject);
            managerObject.AddComponent<LightLODManager>();

            Plugin.Log.LogInfo("[LightLOD] System initialized.");
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPostfix]
        private static void RegisterNewLights(GameObject __result)
        {
            if (!Plugin.LightLODEnabled.Value || LightLODManager.Instance == null || __result == null)
            {
                return;
            }

            var lights = __result.GetComponentsInChildren<Light>(true);
            if (lights != null && lights.Length > 0)
            {
                foreach (var light in lights)
                {
                    if (light != null)
                    {
                        LightLODManager.Instance.RegisterLight(light);
                    }
                }
            }
        }
    }
}
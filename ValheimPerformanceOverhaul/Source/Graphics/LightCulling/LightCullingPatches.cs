using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.LightCulling
{
    [HarmonyPatch]
    public static class LightCullingPatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void AddLightManager_Postfix(Player __instance)
        {
            if (!Plugin.LightCullingEnabled.Value || __instance != Player.m_localPlayer)
            {
                return;
            }

            // Проверяем, не создан ли уже менеджер
            if (AdvancedLightManager.Instance != null) return;

            // Создаем глобальный менеджер
            var managerObject = new GameObject("_VPO_AdvancedLightManager");
            Object.DontDestroyOnLoad(managerObject);
            managerObject.AddComponent<AdvancedLightManager>();

            Plugin.Log.LogInfo("[LightCulling] Advanced manager initialized.");
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPostfix]
        private static void OnObjectInstantiated_Postfix(GameObject __result)
        {
            if (!Plugin.LightCullingEnabled.Value || AdvancedLightManager.Instance == null || __result == null)
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
                        AdvancedLightManager.Instance.TryRegisterLight(light);
                    }
                }
            }
        }
    }
}
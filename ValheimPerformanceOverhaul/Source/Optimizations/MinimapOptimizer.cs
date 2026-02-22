using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
    [HarmonyPatch]
    public static class MinimapOptimizer
    {
                private static FieldInfo _smallRootField = AccessTools.Field(typeof(Minimap), "m_smallRoot");
        private static FieldInfo _largeRootField = AccessTools.Field(typeof(Minimap), "m_largeRoot");

                private static bool _fieldsFound = false;

        static MinimapOptimizer()
        {
                        if (_smallRootField != null && _largeRootField != null)
            {
                _fieldsFound = true;
            }
            else
            {
                                if (_smallRootField == null) _smallRootField = AccessTools.Field(typeof(Minimap), "m_smallMapPanel");
                if (_largeRootField == null) _largeRootField = AccessTools.Field(typeof(Minimap), "m_largeMapPanel");

                _fieldsFound = (_smallRootField != null && _largeRootField != null);

                if (!_fieldsFound)
                {
                    Plugin.Log.LogWarning("[MinimapOptimizer] Could not find Minimap root fields. Optimization disabled.");
                }
            }
        }

        [HarmonyPatch(typeof(Minimap), "Update")]
        [HarmonyPrefix]
        private static bool Minimap_Update_Prefix(Minimap __instance)
        {
                        if (!Plugin.MinimapOptimizationEnabled.Value) return true;
            
                        if (!_fieldsFound) return true;

                                    
                        if (Player.m_localPlayer == null) return true;

                        if (__instance.m_mode == Minimap.MapMode.Large) return true;

                                    if (__instance.m_mode == Minimap.MapMode.Small)
            {
                                GameObject smallRoot = (GameObject)_smallRootField.GetValue(__instance);

                                if (smallRoot != null && !smallRoot.activeInHierarchy)
                {
                    return false;                 }
            }

            return true;
        }

                        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        [HarmonyPrefix]
        private static bool UpdateDynamicPins_Prefix(Minimap __instance)
        {
                        if (!Plugin.MinimapOptimizationEnabled.Value) return true;
            
                        if (__instance.m_mode == Minimap.MapMode.None) return false;

            return true;
        }
    }
}
using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Graphics
{
    [HarmonyPatch]
    public static class LightFlickerPatch
    {
        private static readonly System.Reflection.FieldInfo _fLight =
            AccessTools.Field(typeof(LightFlicker), "m_light");

        private static readonly System.Reflection.FieldInfo _fBaseIntensity =
            AccessTools.Field(typeof(LightFlicker), "m_baseIntensity");

        [HarmonyPatch(typeof(LightFlicker), "CustomUpdate")]
        [HarmonyPrefix]
        private static bool LightFlicker_CustomUpdate_Prefix(LightFlicker __instance)
        {
            if (!Plugin.LightFlickerOptimizationEnabled.Value)
                return true;

            if (__instance == null) return true;

            if (_fLight == null || _fBaseIntensity == null) return true;

            var light = _fLight.GetValue(__instance) as Light;
            if (light == null) return true;

            float baseIntensity = (float)_fBaseIntensity.GetValue(__instance);

            light.intensity = baseIntensity;

            return false; // блокируем оригинальный CustomUpdate
        }
    }
}
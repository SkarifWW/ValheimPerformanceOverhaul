using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Graphics
{

    [HarmonyPatch]
    public static class EngineQualityPatch
    {
        private static bool _applied = false;

        [HarmonyPatch(typeof(GameCamera), "Awake")]
        [HarmonyPostfix]
        private static void ApplyEngineQualitySettings()
        {
            if (!Plugin.EngineQualitySettingsEnabled.Value) return;
            if (_applied) return;
            _applied = true;

            try
            {
                QualitySettings.softParticles = false;

                QualitySettings.softVegetation = false;

                QualitySettings.particleRaycastBudget = Plugin.ParticleRaycastBudget.Value;

                Plugin.Log.LogInfo(
                    $"[EngineQuality] Applied: softParticles=false, softVegetation=false, " +
                    $"particleRaycastBudget={Plugin.ParticleRaycastBudget.Value}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[EngineQuality] Failed to apply settings: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        [HarmonyPostfix]
        private static void ResetAppliedFlag()
        {
            _applied = false;
        }
    }
}
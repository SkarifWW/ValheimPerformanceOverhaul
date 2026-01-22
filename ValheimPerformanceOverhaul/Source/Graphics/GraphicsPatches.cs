using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.PostProcessing;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul
{
    [HarmonyPatch]
    public static class GraphicsPatches
    {
        private static readonly FieldInfo _heightmapsListField = AccessTools.Field(typeof(Heightmap), "s_heightmaps");

        [HarmonyPatch(typeof(GameCamera), "Awake")]
        [HarmonyPostfix]
        private static void ApplyPostProcessingSettings(GameCamera __instance)
        {
            // Проверяем, включен ли модуль в конфиге
            if (!Plugin.GraphicsSettingsEnabled.Value) return;

            var postProcessingBehaviour = __instance.GetComponent<PostProcessingBehaviour>();
            if (postProcessingBehaviour == null || postProcessingBehaviour.profile == null) return;

            var profile = postProcessingBehaviour.profile;
            profile.bloom.enabled = Plugin.ConfigBloom.Value;
            profile.screenSpaceReflection.enabled = Plugin.ConfigReflections.Value;

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo("[Graphics] Applied post-processing settings.");
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void ApplyInitialTerrainSettings()
        {
            // Проверяем, включен ли модуль в конфиге
            if (!Plugin.GraphicsSettingsEnabled.Value) return;

            if (_heightmapsListField == null) return;

            var heightmaps = _heightmapsListField.GetValue(null) as List<Heightmap>;
            if (heightmaps == null) return;

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo($"[Graphics] Applying terrain quality to {heightmaps.Count} existing heightmaps...");

            const float basePixelError = 5f;
            float qualityMultiplier = Mathf.Clamp(Plugin.ConfigTerrainQuality.Value, 0.1f, 2.0f);
            float newPixelError = basePixelError / qualityMultiplier;

            foreach (var heightmap in heightmaps)
            {
                if (heightmap != null)
                {
                    var terrain = heightmap.GetComponent<Terrain>();
                    if (terrain != null)
                    {
                        terrain.heightmapPixelError = newPixelError;
                    }
                }
            }
        }
    }
}
using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Vegetation
{
    [HarmonyPatch]
    public static class VegetationPatches
    {
                [HarmonyPatch(typeof(ClutterSystem), "Awake")]
        [HarmonyPostfix]
        private static void ReduceGrassDistance(ClutterSystem __instance)
        {
            if (!Plugin.VegetationOptimizationEnabled.Value) return;

            try
            {
                                                float distance = Plugin.GrassRenderDistance.Value;

                var clutterDistance = AccessTools.Field(typeof(ClutterSystem), "m_distance");
                if (clutterDistance != null)
                {
                    clutterDistance.SetValue(__instance, distance);
                }

                if (Plugin.DebugLoggingEnabled.Value)
                {
                    Plugin.Log.LogInfo($"[Vegetation] Set grass distance to {distance}m");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Vegetation] Failed to patch grass distance: {e.Message}");
            }
        }

                [HarmonyPatch(typeof(ClutterSystem), "Awake")]
        [HarmonyPostfix]
        private static void ReduceGrassDensity(ClutterSystem __instance)
        {
            if (!Plugin.VegetationOptimizationEnabled.Value) return;

            try
            {
                                float densityMultiplier = Plugin.GrassDensityMultiplier.Value;

                                var clutters = AccessTools.Field(typeof(ClutterSystem), "m_clutter");
                if (clutters != null)
                {
                    var clutterList = clutters.GetValue(__instance) as System.Collections.IList;
                    if (clutterList != null)
                    {
                        foreach (var clutter in clutterList)
                        {
                                                        var amountField = AccessTools.Field(clutter.GetType(), "m_amount");
                            if (amountField != null)
                            {
                                int originalAmount = (int)amountField.GetValue(clutter);
                                int newAmount = Mathf.Max(1, Mathf.RoundToInt(originalAmount * densityMultiplier));
                                amountField.SetValue(clutter, newAmount);
                            }
                        }
                    }
                }

                if (Plugin.DebugLoggingEnabled.Value)
                {
                    Plugin.Log.LogInfo($"[Vegetation] Set grass density to {densityMultiplier * 100}%");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Vegetation] Failed to patch grass density: {e.Message}");
            }
        }

                [HarmonyPatch(typeof(Heightmap), "Awake")]
        [HarmonyPostfix]
        private static void OptimizeTerrainDetails(Heightmap __instance)
        {
            if (!Plugin.VegetationOptimizationEnabled.Value) return;

            try
            {
                var terrain = __instance.GetComponent<Terrain>();
                if (terrain != null)
                {
                                        terrain.detailObjectDistance = Plugin.DetailObjectDistance.Value;
                    terrain.detailObjectDensity = Plugin.DetailDensity.Value;

                                        terrain.heightmapMaximumLOD = Plugin.TerrainMaxLOD.Value;

                    if (Plugin.DebugLoggingEnabled.Value)
                    {
                        Plugin.Log.LogInfo($"[Vegetation] Terrain details optimized: distance={terrain.detailObjectDistance}, density={terrain.detailObjectDensity}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Vegetation] Failed to optimize terrain: {e.Message}");
            }
        }
    }
}
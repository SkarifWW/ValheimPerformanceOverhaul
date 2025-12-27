using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.UI
{
    [HarmonyPatch]
    public static class MinimapPatches
    {
        // ПАТЧ 1: Снижаем разрешение текстуры миникарты
        [HarmonyPatch(typeof(Minimap), "Awake")]
        [HarmonyPostfix]
        private static void ReduceMinimapResolution(Minimap __instance)
        {
            if (!Plugin.MinimapOptimizationEnabled.Value) return;

            try
            {
                int resolution = Plugin.MinimapTextureSize.Value;

                // Получаем поля через рефлексию
                var mapImageLarge = AccessTools.Field(typeof(Minimap), "m_mapImageLarge");
                var mapImageSmall = AccessTools.Field(typeof(Minimap), "m_mapImageSmall");

                if (mapImageLarge != null)
                {
                    var texture = mapImageLarge.GetValue(__instance) as RenderTexture;
                    if (texture != null)
                    {
                        // Пересоздаем с меньшим разрешением
                        var newTexture = new RenderTexture(resolution, resolution, 0);
                        newTexture.name = "MinimapLarge";
                        mapImageLarge.SetValue(__instance, newTexture);

                        // Очищаем старую текстуру
                        Object.Destroy(texture);
                    }
                }

                if (Plugin.DebugLoggingEnabled.Value)
                {
                    Plugin.Log.LogInfo($"[Minimap] Reduced resolution to {resolution}x{resolution}");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Minimap] Failed to optimize: {e.Message}");
            }
        }

        // ПАТЧ 2: Снижаем частоту обновления карты
        [HarmonyPatch(typeof(Minimap), "Update")]
        [HarmonyPrefix]
        private static bool ThrottleMinimapUpdate(Minimap __instance)
        {
            if (!Plugin.MinimapOptimizationEnabled.Value) return true;

            // Обновляем миникарту только каждый N-й кадр
            int updateInterval = Plugin.MinimapUpdateInterval.Value;

            if (Time.frameCount % updateInterval != 0)
            {
                return false; // Пропускаем обновление
            }

            return true; // Выполняем обновление
        }

        // ПАТЧ 3: Отключаем обновление когда карта закрыта
        [HarmonyPatch(typeof(Minimap), "Update")]
        [HarmonyPrefix]
        private static bool DisableWhenClosed(Minimap __instance)
        {
            if (!Plugin.MinimapOptimizationEnabled.Value) return true;

            // Если карта вообще не видна - не обновляем
            var largePanelField = AccessTools.Field(typeof(Minimap), "m_largeMapPanel");
            var smallPanelField = AccessTools.Field(typeof(Minimap), "m_smallMapPanel");

            if (largePanelField != null && smallPanelField != null)
            {
                var largePanel = largePanelField.GetValue(__instance) as GameObject;
                var smallPanel = smallPanelField.GetValue(__instance) as GameObject;

                bool largeActive = largePanel != null && largePanel.activeSelf;
                bool smallActive = smallPanel != null && smallPanel.activeSelf;

                // Если обе панели закрыты - не обновляем
                if (!largeActive && !smallActive)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
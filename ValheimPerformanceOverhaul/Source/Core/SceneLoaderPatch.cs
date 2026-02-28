using HarmonyLib;

namespace ValheimPerformanceOverhaul.Core
{
    [HarmonyPatch]
    public static class SceneLoaderPatch
    {
        [HarmonyPatch(typeof(SceneLoader), "Start")]
        [HarmonyPrefix]
        private static void SceneLoader_Start_Prefix(SceneLoader __instance)
        {
            if (!Plugin.SkipIntroEnabled.Value) return;

            Traverse.Create(__instance)
                    .Field("_showLogos")
                    .SetValue(false);

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo("[SceneLoader] Intro logos skipped.");
        }
    }
}
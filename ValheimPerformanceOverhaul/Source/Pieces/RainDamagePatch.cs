using HarmonyLib;
using BepInEx.Configuration;
using ValheimPerformanceOverhaul;

namespace ValheimPerformanceOverhaul.Pieces
{
    public static class RainDamagePatch
    {
        private static ConfigEntry<bool> _disableRainDamage;

        private static bool IsRainDamageDisabled
        {
            get
            {
                if (_disableRainDamage == null && Plugin.Instance != null)
                {
                    _disableRainDamage = Plugin.Instance.Config.Bind(
                        "10. Piece Optimization",
                        "Disable Rain Damage",
                        false,
                        "Disables rain and water damage calculations for all pieces.");
                }
                return _disableRainDamage?.Value ?? false;
            }
        }

                [HarmonyPatch(typeof(WearNTear), "HaveRoof")]
        [HarmonyPostfix]
        private static void HaveRoof_Postfix(ref bool __result)
        {
            if (Plugin.PieceOptimizationEnabled.Value && IsRainDamageDisabled)
            {
                __result = true;
            }
        }

                [HarmonyPatch(typeof(WearNTear), "IsUnderWater")]
        [HarmonyPostfix]
        private static void IsUnderWater_Postfix(ref bool __result)
        {
            if (Plugin.PieceOptimizationEnabled.Value && IsRainDamageDisabled)
            {
                __result = false;
            }
        }
    }
}

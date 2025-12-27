using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace ValheimPerformanceOverhaul
{
    [HarmonyPatch]
    public static class JitPatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void WarmupGameMethods(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !Plugin.JitWarmupEnabled.Value)
            {
                return;
            }

            try
            {
                Prepare(AccessTools.Method(typeof(Player), "GetHoverObject"), "Player.GetHoverObject");
                Prepare(AccessTools.Method(typeof(Player), "GetHoverCreature"), "Player.GetHoverCreature");
                Prepare(AccessTools.Method(typeof(Player), "GetHoveringPiece"), "Player.GetHoveringPiece");
                Prepare(AccessTools.Method(typeof(EnvMan), "GetCurrentBiome"), "EnvMan.GetCurrentBiome");
                Prepare(AccessTools.Method(typeof(InventoryGui), "Show"), "InventoryGui.Show");
                Prepare(AccessTools.Method(typeof(Character), "Damage"), "Character.Damage");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"JIT Warm-up error: {e}");
            }
        }

        private static void Prepare(MethodInfo method, string methodName)
        {
            if (method != null)
            {
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
            }
        }
    }
}
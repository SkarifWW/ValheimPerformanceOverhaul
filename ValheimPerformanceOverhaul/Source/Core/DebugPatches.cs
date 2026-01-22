using HarmonyLib;
using UnityEngine;
using System;

namespace ValheimPerformanceOverhaul.Core
{
    [HarmonyPatch]
    public static class RigidbodyDebugPatches
    {
        // 1. Patch for set_velocity (Old API, but still used)
        [HarmonyPatch(typeof(Rigidbody), "velocity", MethodType.Setter)]
        [HarmonyPrefix]
        private static bool Prefix_SetVelocity(Rigidbody __instance, Vector3 value)
        {
            if (__instance == null || !__instance.isKinematic || value == Vector3.zero) return true;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogWarning($"[RigidbodyDebug] Attempted to set velocity {value} on KINEMATIC Rigidbody {__instance.name}. StackTrace:\n{Environment.StackTrace}");
            }

            // Optional: Suppress the warning by skipping the native call
            // return false; 
            return true;
        }

        // 2. Patch for set_angularVelocity
        [HarmonyPatch(typeof(Rigidbody), "angularVelocity", MethodType.Setter)]
        [HarmonyPrefix]
        private static bool Prefix_SetAngularVelocity(Rigidbody __instance, Vector3 value)
        {
            if (__instance == null || !__instance.isKinematic || value == Vector3.zero) return true;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogWarning($"[RigidbodyDebug] Attempted to set angularVelocity {value} on KINEMATIC Rigidbody {__instance.name}. StackTrace:\n{Environment.StackTrace}");
            }

            return true;
        }

        // 3. Patch for new Unity 2022.3 API: linearVelocity
        // We use string match here because it might not exist in all Unity versions (though it should in Valheim now)
        [HarmonyPatch(typeof(Rigidbody), "linearVelocity", MethodType.Setter)]
        [HarmonyPrefix]
        private static bool Prefix_SetLinearVelocity(Rigidbody __instance, Vector3 value)
        {
            if (__instance == null || !__instance.isKinematic || value == Vector3.zero) return true;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogWarning($"[RigidbodyDebug] Attempted to set linearVelocity {value} on KINEMATIC Rigidbody {__instance.name}. StackTrace:\n{Environment.StackTrace}");
            }

            return true;
        }

        // 4. Catch the Unity warning directly from the logger
        [HarmonyPatch(typeof(Debug), "LogWarning", new Type[] { typeof(object) })]
        [HarmonyPrefix]
        private static void Prefix_CatchUnityWarning(object message)
        {
            if (message == null || !Plugin.DebugLoggingEnabled.Value) return;

            string msg = message.ToString();
            if (msg.Contains("linear velocity") && msg.Contains("kinematic"))
            {
                Plugin.Log.LogWarning($"[RigidbodyDebug] CAUGHT UNITY WARNING: {msg}\nSTACKTRACE:\n{Environment.StackTrace}");
            }
        }
    }
}

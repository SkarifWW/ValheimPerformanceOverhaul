using HarmonyLib;
using UnityEngine;
using System;

namespace ValheimPerformanceOverhaul.Core
{
    [HarmonyPatch]
    public static class RigidbodyDebugPatches
    {
                [HarmonyPatch(typeof(Rigidbody), "velocity", MethodType.Setter)]
        [HarmonyPrefix]
        private static bool Prefix_SetVelocity(Rigidbody __instance, Vector3 value)
        {
            if (__instance == null || !__instance.isKinematic || value == Vector3.zero) return true;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogWarning($"[RigidbodyDebug] Attempted to set velocity {value} on KINEMATIC Rigidbody {__instance.name}. StackTrace:\n{Environment.StackTrace}");
            }

                                    return true;
        }

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

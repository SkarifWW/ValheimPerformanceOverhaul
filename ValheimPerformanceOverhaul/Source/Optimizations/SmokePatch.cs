using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Optimizations
{
    [HarmonyPatch]
    public static class SmokePatch
    {
        private static readonly System.Reflection.FieldInfo _fBody =
            AccessTools.Field(typeof(Smoke), "m_body");

        private static readonly System.Reflection.FieldInfo _fTime =
            AccessTools.Field(typeof(Smoke), "m_time");

        private static readonly System.Reflection.FieldInfo _fTtl =
            AccessTools.Field(typeof(Smoke), "m_ttl");

        private static readonly System.Reflection.FieldInfo _fMr =
            AccessTools.Field(typeof(Smoke), "m_mr");

        [HarmonyPatch(typeof(Smoke), "CustomUpdate")]
        [HarmonyPrefix]
        private static bool Smoke_CustomUpdate_Prefix(Smoke __instance)
        {
            if (!Plugin.SmokeOptimizationEnabled.Value)
                return true;

            if (__instance == null) return true;

            if (_fBody == null || _fTime == null || _fTtl == null) return true;

            var body = _fBody.GetValue(__instance) as Rigidbody;
            if (body == null) return true;

            float time = (float)_fTime.GetValue(__instance);
            float ttl = (float)_fTtl.GetValue(__instance);

            if (ttl <= 0f) return false;

            float remaining = 1f - time / ttl;

            float mass = remaining * remaining;
            body.mass = Mathf.Max(mass, 0.001f);

            Vector3 lift = Vector3.up * Plugin.SmokeLiftForce.Value * (1f - mass);
            body.AddForce(lift, ForceMode.Acceleration);

            float newTime = time + Time.fixedDeltaTime;
            _fTime.SetValue(__instance, newTime);

            if (newTime >= ttl && _fMr != null)
            {
                var mr = _fMr.GetValue(__instance) as MeshRenderer;
                if (mr != null) mr.enabled = false;
            }

            return false; // блокируем оригинальный CustomUpdate
        }
    }
}
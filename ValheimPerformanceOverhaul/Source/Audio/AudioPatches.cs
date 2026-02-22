using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Audio
{
    [HarmonyPatch]
    public static class AudioPatches
    {
                [HarmonyPatch(typeof(ZSFX), "Play")]
        [HarmonyPrefix]
        private static bool ZSFX_Play_Prefix(ZSFX __instance)
        {
            if (!Plugin.AudioPoolingEnabled.Value) return true;

            var audioSource = __instance.GetComponent<AudioSource>();
            if (audioSource == null) return true;

                        if (AudioPoolManager.TryPlayClip(audioSource))
            {
                                return false;
            }

                        return true;
        }

                [HarmonyPatch(typeof(AudioSource), "Play", new System.Type[0])]
        [HarmonyPrefix]
        private static bool AudioSource_Play_Prefix(AudioSource __instance)
        {
            if (!Plugin.AudioPoolingEnabled.Value) return true;

                        if (__instance.GetComponent<ZSFX>() != null) return true;

                        if (__instance.GetComponent<PooledAudio>() != null) return true;

                                    if (__instance.spatialBlend < 0.1f)
            {
                return true;
            }

                        if (__instance.outputAudioMixerGroup != null)
            {
                string groupName = __instance.outputAudioMixerGroup.name.ToLower();
                if (groupName.Contains("music") || groupName.Contains("gui"))
                {
                    return true;
                }
            }

                        if (__instance.loop)
            {
                return true;
            }

                        if (AudioPoolManager.TryPlayClip(__instance))
            {
                return false;
            }

                        return true;
        }

                [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new System.Type[] { typeof(AudioClip), typeof(float) })]
        [HarmonyPrefix]
        private static bool AudioSource_PlayOneShot_Prefix(AudioSource __instance, AudioClip clip, float volumeScale)
        {
            if (!Plugin.AudioPoolingEnabled.Value) return true;

                                    return true;
        }
    }
}
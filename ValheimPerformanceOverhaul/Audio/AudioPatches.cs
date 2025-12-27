using HarmonyLib;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Audio
{
    [HarmonyPatch]
    public static class AudioPatches
    {
        // Патч для ZSFX (основной способ воспроизведения звуков в Valheim)
        [HarmonyPatch(typeof(ZSFX), "Play")]
        [HarmonyPrefix]
        private static bool ZSFX_Play_Prefix(ZSFX __instance)
        {
            if (!Plugin.AudioPoolingEnabled.Value) return true;

            var audioSource = __instance.GetComponent<AudioSource>();
            if (audioSource == null) return true;

            // ВАЖНО: Пытаемся воспроизвести через пул
            if (AudioPoolManager.TryPlayClip(audioSource))
            {
                // Успешно воспроизвели через пул - блокируем оригинальный метод
                return false;
            }

            // Пул не смог обработать - пропускаем к оригинальному методу
            return true;
        }

        // Дополнительный патч для прямых вызовов AudioSource.Play()
        [HarmonyPatch(typeof(AudioSource), "Play", new System.Type[0])]
        [HarmonyPrefix]
        private static bool AudioSource_Play_Prefix(AudioSource __instance)
        {
            if (!Plugin.AudioPoolingEnabled.Value) return true;

            // Пропускаем, если уже обработан через ZSFX
            if (__instance.GetComponent<ZSFX>() != null) return true;

            // Пропускаем объекты из пула (избегаем рекурсии)
            if (__instance.GetComponent<PooledAudio>() != null) return true;

            // ВАЖНО: Фильтруем что пулить
            // Не пулим 2D звуки
            if (__instance.spatialBlend < 0.1f)
            {
                return true;
            }

            // Не пулим музыку и GUI
            if (__instance.outputAudioMixerGroup != null)
            {
                string groupName = __instance.outputAudioMixerGroup.name.ToLower();
                if (groupName.Contains("music") || groupName.Contains("gui"))
                {
                    return true;
                }
            }

            // Не пулим зацикленные звуки
            if (__instance.loop)
            {
                return true;
            }

            // Пытаемся воспроизвести через пул
            if (AudioPoolManager.TryPlayClip(__instance))
            {
                return false;
            }

            // Пул не справился - используем оригинальный метод
            return true;
        }

        // НОВЫЙ ПАТЧ: Обработка AudioSource.PlayOneShot
        [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new System.Type[] { typeof(AudioClip), typeof(float) })]
        [HarmonyPrefix]
        private static bool AudioSource_PlayOneShot_Prefix(AudioSource __instance, AudioClip clip, float volumeScale)
        {
            if (!Plugin.AudioPoolingEnabled.Value) return true;

            // PlayOneShot не пулим - он используется для коротких звуков и работает иначе
            // Оставляем его как есть для стабильности
            return true;
        }
    }
}
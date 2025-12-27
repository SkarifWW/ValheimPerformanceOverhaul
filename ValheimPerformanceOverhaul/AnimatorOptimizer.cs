using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine;
namespace ValheimPerformanceOverhaul.Animators
{
    [HarmonyPatch]
    public static class AnimatorPatches
    {
        private static readonly Dictionary<Animator, AnimatorCullingMode> _originalCulling =
            new Dictionary<Animator, AnimatorCullingMode>();

        // ПАТЧ 1: Установка правильного Culling Mode для всех Animator
        [HarmonyPatch(typeof(Character), "Awake")]
        [HarmonyPostfix]
        private static void OptimizeAnimator(Character __instance)
        {
            if (!Plugin.AnimatorOptimizationEnabled.Value) return;

            var animator = __instance.GetComponentInChildren<Animator>();
            if (animator == null) return;

            // Сохраняем оригинальный режим
            if (!_originalCulling.ContainsKey(animator))
            {
                _originalCulling[animator] = animator.cullingMode;
            }

            // КРИТИЧНО: Устанавливаем CullCompletely
            // Это полностью отключает анимацию когда объект вне экрана
            animator.cullingMode = AnimatorCullingMode.CullCompletely;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[Animator] Optimized animator for {__instance.name}");
            }
        }

        // ПАТЧ 2: Снижаем Update Rate для далеких аниматоров
        [HarmonyPatch(typeof(Character), "Update")]
        [HarmonyPrefix]
        private static void ThrottleDistantAnimators(Character __instance)
        {
            if (!Plugin.AnimatorOptimizationEnabled.Value || Player.m_localPlayer == null) return;

            var animator = __instance.GetComponentInChildren<Animator>();
            if (animator == null) return;

            float distance = Vector3.Distance(__instance.transform.position, Player.m_localPlayer.transform.position);

            // Далекие персонажи - обновляем анимацию реже
            if (distance > 30f)
            {
                // Обновляем только каждый 3й кадр
                if (Time.frameCount % 3 != 0)
                {
                    animator.enabled = false;
                }
                else
                {
                    animator.enabled = true;
                }
            }
            else if (distance > 15f)
            {
                // Средняя дистанция - каждый 2й кадр
                if (Time.frameCount % 2 != 0)
                {
                    animator.enabled = false;
                }
                else
                {
                    animator.enabled = true;
                }
            }
            else
            {
                // Близко - полная частота
                animator.enabled = true;
            }
        }

        // Очистка при уничтожении
        [HarmonyPatch(typeof(Character), "OnDestroy")]
        [HarmonyPostfix]
        private static void RestoreAnimator(Character __instance)
        {
            var animator = __instance.GetComponentInChildren<Animator>();
            if (animator != null && _originalCulling.ContainsKey(animator))
            {
                animator.cullingMode = _originalCulling[animator];
                _originalCulling.Remove(animator);
            }
        }
    }
}
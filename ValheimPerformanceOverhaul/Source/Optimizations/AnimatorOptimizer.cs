using HarmonyLib;
using UnityEngine;
using ValheimPerformanceOverhaul;

namespace ValheimPerformanceOverhaul
{
    public static class AnimatorOptimizer
    {
        [HarmonyPatch(typeof(Character), "Awake")]
        public static class Character_Awake_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Character __instance)
            {
                if (__instance == null) return;

                if (!Plugin.AnimatorOptimizationEnabled.Value) return;

                if (__instance.GetComponent<CharacterAnimatorOptimizer>() == null)
                {
                    __instance.gameObject.AddComponent<CharacterAnimatorOptimizer>();
                }
            }
        }
    }

    public class CharacterAnimatorOptimizer : MonoBehaviour
    {
        private Character _character;
        private Animator _animator;
        private ZNetView _nview;
        private float _checkTimer;

        private const float CHECK_INTERVAL = 1.0f;
        private const float CULL_DIST_SQR = 60f * 60f;
        private const float FAR_CULL_DIST_SQR = 100f * 100f; 
        private bool _isFullyCulled = false;
        private bool _isPartiallyCulled = false;

        private void Awake()
        {
            _character = GetComponent<Character>();
            _animator = GetComponent<Animator>();
            _nview = GetComponent<ZNetView>();

            _checkTimer = Random.Range(0f, CHECK_INTERVAL);
        }

        private void FixedUpdate()
        {
            _checkTimer += Time.fixedDeltaTime;
            if (_checkTimer < CHECK_INTERVAL) return;
            _checkTimer = 0f;

            Optimize();
        }

        private void Optimize()
        {
            if (_character == null || _animator == null)
            {
                Destroy(this);
                return;
            }

            if (_character.IsPlayer()) return;

            if (_nview != null && !_nview.IsValid()) return;

            if (Player.m_localPlayer == null) return;

            float distSqr = (_character.transform.position - Player.m_localPlayer.transform.position).sqrMagnitude;

                        if (distSqr > FAR_CULL_DIST_SQR)
            {
                                if (!_isFullyCulled)
                {
                    _animator.enabled = false;                     _isFullyCulled = true;
                    _isPartiallyCulled = false;

                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[Animator] Fully disabled for {_character.name} at {Mathf.Sqrt(distSqr):F1}m");
                }
            }
            else if (distSqr > CULL_DIST_SQR)
            {
                                if (!_isPartiallyCulled || _isFullyCulled)
                {
                    _animator.enabled = true;                     _animator.cullingMode = AnimatorCullingMode.CullCompletely;                     _isPartiallyCulled = true;
                    _isFullyCulled = false;

                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[Animator] CullCompletely for {_character.name} at {Mathf.Sqrt(distSqr):F1}m");
                }
            }
            else
            {
                                if (_isPartiallyCulled || _isFullyCulled)
                {
                    _animator.enabled = true;
                    _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    _isPartiallyCulled = false;
                    _isFullyCulled = false;

                    if (Plugin.DebugLoggingEnabled.Value)
                        Plugin.Log.LogInfo($"[Animator] Full animation for {_character.name} at {Mathf.Sqrt(distSqr):F1}m");
                }
            }
        }

        private void OnDestroy()
        {
                        if (_animator != null)
            {
                _animator.enabled = true;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }
    }
}
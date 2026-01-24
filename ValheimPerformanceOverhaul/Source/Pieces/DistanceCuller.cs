using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
    public class DistanceCuller : MonoBehaviour
    {
        private readonly List<MonoBehaviour> _culledComponents = new List<MonoBehaviour>();

        // ✅ КРИТИЧНО: Минимальное хранение данных о Rigidbody
        private readonly List<Rigidbody> _trackedRigidbodies = new List<Rigidbody>();

        private ZNetView _zNetView;
        private bool _isCulled = false;
        public float CullDistance = 80f;

        private float _cullDistanceSqr;
        private float _wakeUpDistanceSqr;
        private const float HYSTERESIS = 15f;
        private const float CHECK_INTERVAL = 2.0f;
        private float _checkTimer;

        private Transform _transform;

        // ✅ НОВОЕ: Флаг для определения типа объекта
        private bool _isCharacter = false;

        private void Awake()
        {
            _transform = transform;
            _zNetView = GetComponent<ZNetView>();

            if (_zNetView == null || !_zNetView.IsValid())
            {
                Destroy(this);
                return;
            }

            // ✅ КРИТИЧНО: Определяем тип ОДИН раз
            _isCharacter = GetComponent<Character>() != null;

            try
            {
                _cullDistanceSqr = (CullDistance + HYSTERESIS) * (CullDistance + HYSTERESIS);
                _wakeUpDistanceSqr = (CullDistance - HYSTERESIS) * (CullDistance - HYSTERESIS);

                CollectComponents();

                // ✅ КРИТИЧНО: Собираем Rigidbody ТОЛЬКО для Piece
                if (Plugin.CullPhysicsEnabled.Value && !_isCharacter)
                {
                    CollectRigidbodies();
                }

                _checkTimer = Random.Range(0f, CHECK_INTERVAL);
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogError($"[DistanceCuller] Error in Awake: {e.Message}");
                Destroy(this);
            }
        }

        private void CollectComponents()
        {
            var components = GetComponentsInChildren<MonoBehaviour>(true);
            if (components == null) return;

            foreach (var component in components)
            {
                if (component == null) continue;

                if (component == this ||
                    component is ZNetView ||
                    component is ZSyncTransform ||
                    component is DistanceCuller ||
                    component is Character ||
                    component is Humanoid)
                {
                    continue;
                }

                if (Plugin.AiThrottlingEnabled.Value && component is BaseAI)
                {
                    continue;
                }

                _culledComponents.Add(component);
            }
        }

        private void CollectRigidbodies()
        {
            var rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            if (rigidbodies == null) return;

            foreach (var rb in rigidbodies)
            {
                if (rb != null && !_trackedRigidbodies.Contains(rb))
                {
                    _trackedRigidbodies.Add(rb);
                }
            }

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo($"[DistanceCuller] Collected {_trackedRigidbodies.Count} Rigidbodies on {gameObject.name}");
        }

        private void Update()
        {
            // ✅ КРИТИЧНО: Проверяем существование менеджера
            if (!Plugin.DistanceCullerEnabled.Value || DistanceCullerManager.Instance == null)
            {
                // Если функция выключена или менеджер не существует - отключаем компонент
                enabled = false;
                return;
            }

            _checkTimer += Time.deltaTime;
            if (_checkTimer < CHECK_INTERVAL) return;
            _checkTimer = 0f;

            try
            {
                UpdateCullingState();
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogError($"[DistanceCuller] Error in Update: {e.Message}");
            }
        }

        private void UpdateCullingState()
        {
            var players = DistanceCullerManager.Players;

            if (players == null || players.Count == 0)
            {
                if (_isCulled)
                {
                    SetComponentsEnabled(true);
                }
                return;
            }

            float minDistanceSqr = GetMinPlayerDistanceSqr(players);
            bool shouldBeCulled = DetermineCullingState(minDistanceSqr);
            ApplyOwnershipLogic(shouldBeCulled);
        }

        private float GetMinPlayerDistanceSqr(IReadOnlyList<Player> players)
        {
            float minDistanceSqr = float.MaxValue;
            Vector3 myPosition = _transform.position;

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.transform == null) continue;

                float distSqr = (player.transform.position - myPosition).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                }
            }

            return minDistanceSqr;
        }

        private bool DetermineCullingState(float distanceSqr)
        {
            if (_isCulled)
            {
                return distanceSqr > _wakeUpDistanceSqr;
            }
            else
            {
                return distanceSqr > _cullDistanceSqr;
            }
        }

        private void ApplyOwnershipLogic(bool shouldBeCulled)
        {
            if (_zNetView == null) return;

            if (_zNetView.IsOwner())
            {
                if (_isCulled != shouldBeCulled)
                {
                    SetComponentsEnabled(!shouldBeCulled);
                }
            }
            else
            {
                if (_isCulled)
                {
                    SetComponentsEnabled(true);
                }
            }
        }

        private void SetComponentsEnabled(bool enabled)
        {
            bool newStateIsCulled = !enabled;
            if (_isCulled == newStateIsCulled) return;

            _isCulled = newStateIsCulled;

            // Компоненты
            for (int i = _culledComponents.Count - 1; i >= 0; i--)
            {
                var component = _culledComponents[i];

                if (component == null)
                {
                    _culledComponents.RemoveAt(i);
                    continue;
                }

                if (component.enabled != enabled)
                {
                    try
                    {
                        component.enabled = enabled;
                    }
                    catch (System.Exception e)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogWarning($"[DistanceCuller] Failed to set enabled on {component.GetType().Name}: {e.Message}");
                    }
                }
            }

            // ✅ КРИТИЧНО: Rigidbody обработка ТОЛЬКО для Piece
            if (Plugin.CullPhysicsEnabled.Value && !_isCharacter && _trackedRigidbodies.Count > 0)
            {
                for (int i = _trackedRigidbodies.Count - 1; i >= 0; i--)
                {
                    Rigidbody rb = _trackedRigidbodies[i];

                    if (rb == null)
                    {
                        _trackedRigidbodies.RemoveAt(i);
                        continue;
                    }

                    try
                    {
                        if (enabled)
                        {
                            // ✅ Просто включаем обратно
                            // НЕ меняем kinematic - оставляем как было
                            rb.WakeUp();
                        }
                        else
                        {
                            // ✅ КРИТИЧНО: ТОЛЬКО усыпляем, НЕ меняем kinematic
                            if (!rb.isKinematic && !rb.IsSleeping())
                            {
                                rb.Sleep();
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogWarning($"[DistanceCuller] Failed to set Rigidbody state: {e.Message}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_isCulled)
                {
                    SetComponentsEnabled(true);
                }

                // ✅ КРИТИЧНО: Полная очистка
                _culledComponents.Clear();
                _trackedRigidbodies.Clear();
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogError($"[DistanceCuller] Error in OnDestroy: {e.Message}");
            }
        }
    }
}
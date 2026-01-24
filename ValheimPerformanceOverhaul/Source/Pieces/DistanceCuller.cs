using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
    public class DistanceCuller : MonoBehaviour
    {
        private readonly List<MonoBehaviour> _culledComponents = new List<MonoBehaviour>();
        private readonly Dictionary<Rigidbody, RigidbodyState> _culledRigidbodies = new Dictionary<Rigidbody, RigidbodyState>();

        private ZNetView _zNetView;
        private bool _isCulled = false;
        public float CullDistance = 80f;

        private float _cullDistanceSqr;
        private float _wakeUpDistanceSqr;
        private const float HYSTERESIS = 15f; // ✅ Увеличено с 10f для плавности
        private const float CHECK_INTERVAL = 2.0f; // ✅ Увеличено с 1.0f
        private float _checkTimer;

        private float _aiUpdateTimer = 0f;
        private const float AI_THROTTLE_INTERVAL = 1.0f;

        private Transform _transform;

        private struct RigidbodyState
        {
            public bool WasKinematic;
            public bool WasSleeping;
            public Vector3 LastVelocity;
            public Vector3 LastAngularVelocity;
        }

        private readonly List<Rigidbody> _deadRigidbodies = new List<Rigidbody>(8);

        private void Awake()
        {
            _transform = transform;
            _zNetView = GetComponent<ZNetView>();

            if (_zNetView == null || !_zNetView.IsValid())
            {
                Destroy(this);
                return;
            }

            try
            {
                _cullDistanceSqr = (CullDistance + HYSTERESIS) * (CullDistance + HYSTERESIS);
                _wakeUpDistanceSqr = (CullDistance - HYSTERESIS) * (CullDistance - HYSTERESIS);

                CollectComponents();

                if (Plugin.CullPhysicsEnabled.Value)
                {
                    CollectRigidbodies();
                }

                // ✅ Рандомизируем таймер для распределения нагрузки
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
                    component is Rigidbody ||
                    component is Collider ||
                    component is DistanceCuller)
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
                if (rb != null && !_culledRigidbodies.ContainsKey(rb))
                {
                    var state = new RigidbodyState
                    {
                        WasKinematic = rb.isKinematic,
                        WasSleeping = rb.IsSleeping(),
                        LastVelocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity,
                        LastAngularVelocity = rb.isKinematic ? Vector3.zero : rb.angularVelocity
                    };

                    _culledRigidbodies.Add(rb, state);
                }
            }
        }

        private void Update()
        {
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

        public bool ShouldUpdateAI()
        {
            if (!_isCulled) return true;

            _aiUpdateTimer += CHECK_INTERVAL;
            if (_aiUpdateTimer >= AI_THROTTLE_INTERVAL)
            {
                _aiUpdateTimer = 0f;
                return true;
            }

            return false;
        }

        private void SetComponentsEnabled(bool enabled)
        {
            bool newStateIsCulled = !enabled;
            if (_isCulled == newStateIsCulled) return;

            _isCulled = newStateIsCulled;

            // ✅ НЕ спамим логами каждый раз
            if (Plugin.DebugLoggingEnabled.Value && Time.frameCount % 300 == 0)
            {
                LogCullingStateChange(enabled);
            }

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

            // ✅ ИСПРАВЛЕНО: Rigidbody обработка
            if (Plugin.CullPhysicsEnabled.Value)
            {
                _deadRigidbodies.Clear();

                foreach (var pair in _culledRigidbodies)
                {
                    Rigidbody rb = pair.Key;
                    RigidbodyState originalState = pair.Value;

                    if (rb == null)
                    {
                        _deadRigidbodies.Add(rb);
                        continue;
                    }

                    try
                    {
                        if (enabled)
                        {
                            // ✅ Восстанавливаем оригинальное состояние
                            if (!originalState.WasKinematic && rb.isKinematic)
                            {
                                rb.isKinematic = false;

                                // ✅ КРИТИЧНО: Устанавливаем velocity ТОЛЬКО на non-kinematic
                                if (!rb.isKinematic && !originalState.WasSleeping)
                                {
                                    rb.linearVelocity = originalState.LastVelocity;
                                    rb.angularVelocity = originalState.LastAngularVelocity;
                                }
                            }
                        }
                        else
                        {
                            // ✅ КРИТИЧНО: Обнуляем velocity ПЕРЕД установкой kinematic
                            if (!rb.isKinematic)
                            {
                                // Сохраняем текущее состояние
                                var newState = new RigidbodyState
                                {
                                    WasKinematic = originalState.WasKinematic,
                                    WasSleeping = rb.IsSleeping(),
                                    LastVelocity = rb.linearVelocity,
                                    LastAngularVelocity = rb.angularVelocity
                                };
                                _culledRigidbodies[rb] = newState;

                                // ✅ Обнуляем velocity ПЕРЕД kinematic
                                rb.linearVelocity = Vector3.zero;
                                rb.angularVelocity = Vector3.zero;
                                rb.Sleep();

                                // ✅ ТЕПЕРЬ делаем kinematic
                                rb.isKinematic = true;
                            }
                            else if (!originalState.WasKinematic)
                            {
                                // Объект стал kinematic до нас - делаем kinematic
                                rb.isKinematic = true;
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogWarning($"[DistanceCuller] Failed to set Rigidbody state on {rb.gameObject.name}: {e.Message}");
                    }
                }

                // Очистка мертвых ссылок
                if (_deadRigidbodies.Count > 0)
                {
                    foreach (var rb in _deadRigidbodies)
                    {
                        _culledRigidbodies.Remove(rb);
                    }
                }
            }
        }

        private void LogCullingStateChange(bool enabled)
        {
            if (Player.m_localPlayer == null || Player.m_localPlayer.transform == null)
            {
                Plugin.Log.LogInfo($"[DistanceCuller] {(enabled ? "Enabling" : "Disabling")} on {gameObject.name}");
                return;
            }

            float distance = Vector3.Distance(_transform.position, Player.m_localPlayer.transform.position);
            Plugin.Log.LogInfo($"[DistanceCuller] {(enabled ? "Enabling" : "Disabling")} on {gameObject.name} at distance {distance:F1}m");
        }

        private void OnDestroy()
        {
            try
            {
                if (_isCulled)
                {
                    SetComponentsEnabled(true);
                }

                _culledComponents.Clear();
                _culledRigidbodies.Clear();
                _deadRigidbodies.Clear();
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogError($"[DistanceCuller] Error in OnDestroy: {e.Message}");
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
    public class DistanceCuller : MonoBehaviour
    {
        private readonly List<MonoBehaviour> _culledComponents = new List<MonoBehaviour>();
        private readonly Dictionary<Rigidbody, bool> _culledRigidbodies = new Dictionary<Rigidbody, bool>();

        private ZNetView _zNetView;
        private bool _isCulled = false;
        public float CullDistance = 80f;

        private float _cullDistanceSqr;
        private float _wakeUpDistanceSqr;
        private const float HYSTERESIS = 10f;
        private const float CHECK_INTERVAL = 1.0f;
        private float _checkTimer;

        private float _aiUpdateTimer = 0f;
        private const float AI_THROTTLE_INTERVAL = 1.0f;

        private Transform _transform;

        // ✅ НОВОЕ: Флаг для определения типа объекта
        private bool _isCharacter = false;
        private bool _isPiece = false;

        private void Awake()
        {
            _transform = transform;
            _zNetView = GetComponent<ZNetView>();

            if (_zNetView == null || !_zNetView.IsValid())
            {
                Destroy(this);
                return;
            }

            // ✅ КРИТИЧНО: Определяем тип объекта
            _isCharacter = GetComponent<Character>() != null;
            _isPiece = GetComponent<Piece>() != null;

            try
            {
                _cullDistanceSqr = (CullDistance + HYSTERESIS) * (CullDistance + HYSTERESIS);
                _wakeUpDistanceSqr = (CullDistance - HYSTERESIS) * (CullDistance - HYSTERESIS);

                CollectComponents();

                // ✅ КРИТИЧНО: НЕ собираем Rigidbody у Character
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

                // ✅ КРИТИЧНО: Расширенный список исключений
                if (component == this ||
                    component is ZNetView ||
                    component is ZSyncTransform ||
                    component is Rigidbody ||
                    component is Collider ||
                    component is Animator ||
                    component is Character ||      // ✅ НОВОЕ
                    component is Humanoid ||       // ✅ НОВОЕ
                    component is Player)           // ✅ НОВОЕ
                {
                    continue;
                }

                // ✅ НОВОЕ: Не отключаем MonsterAI и AnimalAI полностью
                // Только throttling через ShouldUpdateAI()
                if (Plugin.AiThrottlingEnabled.Value && component is BaseAI)
                {
                    continue;
                }

                _culledComponents.Add(component);
            }
        }

        private void CollectRigidbodies()
        {
            // ✅ КРИТИЧНО: Этот метод вызывается ТОЛЬКО для Piece, НЕ для Character
            var rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            if (rigidbodies == null) return;

            foreach (var rb in rigidbodies)
            {
                if (rb != null && !_culledRigidbodies.ContainsKey(rb))
                {
                    _culledRigidbodies.Add(rb, rb.isKinematic);
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

            if (Plugin.DebugLoggingEnabled.Value)
            {
                LogCullingStateChange(enabled);
            }

            // Отключаем/включаем компоненты
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

            // ✅ КРИТИЧНО: Физику обрабатываем ТОЛЬКО для Piece
            if (Plugin.CullPhysicsEnabled.Value && !_isCharacter)
            {
                List<Rigidbody> toRemove = null;

                foreach (var pair in _culledRigidbodies)
                {
                    Rigidbody rb = pair.Key;
                    bool originalIsKinematic = pair.Value;

                    if (rb == null)
                    {
                        if (toRemove == null)
                            toRemove = new List<Rigidbody>();
                        toRemove.Add(rb);
                        continue;
                    }

                    try
                    {
                        rb.isKinematic = enabled ? originalIsKinematic : true;
                    }
                    catch (System.Exception e)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogWarning($"[DistanceCuller] Failed to set isKinematic: {e.Message}");
                    }
                }

                if (toRemove != null)
                {
                    foreach (var rb in toRemove)
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
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogError($"[DistanceCuller] Error in OnDestroy: {e.Message}");
            }
        }
    }
}
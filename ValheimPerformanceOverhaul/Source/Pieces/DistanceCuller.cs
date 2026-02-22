using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul
{
    // =========================================================================
    // DistanceCuller больше НЕ имеет Update().
    // Вся логика вызывается из DistanceCullerManager.Update() — одного цикла
    // на все объекты. 300 блоков = 1 Update() вместо 300.
    // =========================================================================
    public class DistanceCuller : MonoBehaviour
    {
        private readonly List<MonoBehaviour> _culledComponents = new List<MonoBehaviour>();
        private readonly List<Rigidbody> _trackedRigidbodies = new List<Rigidbody>();

        private ZNetView _zNetView;
        private bool _isCulled = false;
        private bool _isCharacter = false;
        private Transform _transform;

        public float CullDistance = 80f;
        private float _cullDistanceSqr;
        private float _wakeUpDistanceSqr;
        private const float HYSTERESIS = 15f;

        private void Awake()
        {
            _transform = transform;
            _zNetView = GetComponent<ZNetView>();

            if (_zNetView == null || !_zNetView.IsValid())
            {
                Destroy(this);
                return;
            }

            _isCharacter = GetComponent<Character>() != null;

            try
            {
                _cullDistanceSqr = (CullDistance + HYSTERESIS) * (CullDistance + HYSTERESIS);
                _wakeUpDistanceSqr = (CullDistance - HYSTERESIS) * (CullDistance - HYSTERESIS);

                CollectComponents();

                if (Plugin.CullPhysicsEnabled.Value && !_isCharacter)
                    CollectRigidbodies();

                // Регистрируемся в менеджере — он будет вызывать ManagerUpdate().
                DistanceCullerManager.Instance?.RegisterCuller(this);
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
                if (component == this) continue;
                if (component is ZNetView) continue;
                if (component is ZSyncTransform) continue;
                if (component is DistanceCuller) continue;
                if (component is Character) continue;
                if (component is Humanoid) continue;
                if (Plugin.AiThrottlingEnabled.Value &&
                    component is BaseAI) continue;

                _culledComponents.Add(component);
            }
        }

        private void CollectRigidbodies()
        {
            var rbs = GetComponentsInChildren<Rigidbody>(true);
            if (rbs == null) return;

            foreach (var rb in rbs)
                if (rb != null && !_trackedRigidbodies.Contains(rb))
                    _trackedRigidbodies.Add(rb);

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo(
                    $"[DistanceCuller] Collected {_trackedRigidbodies.Count} Rigidbodies on {gameObject.name}");
        }

        // Вызывается из DistanceCullerManager — НЕ из Unity Update().
        public void ManagerUpdate(IReadOnlyList<Player> players)
        {
            if (players == null || players.Count == 0)
            {
                if (_isCulled) SetComponentsEnabled(true);
                return;
            }

            float minDistSqr = GetMinPlayerDistanceSqr(players);
            bool shouldCull = DetermineCullingState(minDistSqr);
            ApplyOwnershipLogic(shouldCull);
        }

        private float GetMinPlayerDistanceSqr(IReadOnlyList<Player> players)
        {
            float min = float.MaxValue;
            Vector3 pos = _transform.position;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.transform == null) continue;

                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < min) min = d;
            }
            return min;
        }

        private bool DetermineCullingState(float distSqr)
        {
            // Гистерезис: выходим из culled-состояния только когда игрок
            // подошёл достаточно близко, чтобы не флипать туда-обратно на границе.
            return _isCulled
                ? distSqr > _wakeUpDistanceSqr
                : distSqr > _cullDistanceSqr;
        }

        private void ApplyOwnershipLogic(bool shouldCull)
        {
            if (_zNetView == null) return;

            if (_zNetView.IsOwner())
            {
                if (_isCulled != shouldCull)
                    SetComponentsEnabled(!shouldCull);
            }
            else
            {
                // Не-владелец никогда не должен быть заспан нами —
                // его состоянием управляет владелец.
                if (_isCulled)
                    SetComponentsEnabled(true);
            }
        }

        private void SetComponentsEnabled(bool enabled)
        {
            if (_isCulled == !enabled) return;
            _isCulled = !enabled;

            for (int i = _culledComponents.Count - 1; i >= 0; i--)
            {
                var c = _culledComponents[i];
                if (c == null) { _culledComponents.RemoveAt(i); continue; }

                if (c.enabled != enabled)
                {
                    try { c.enabled = enabled; }
                    catch (System.Exception e)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogWarning(
                                $"[DistanceCuller] Failed to set {c.GetType().Name}: {e.Message}");
                    }
                }
            }

            if (Plugin.CullPhysicsEnabled.Value && !_isCharacter)
            {
                for (int i = _trackedRigidbodies.Count - 1; i >= 0; i--)
                {
                    var rb = _trackedRigidbodies[i];
                    if (rb == null) { _trackedRigidbodies.RemoveAt(i); continue; }

                    try
                    {
                        if (enabled) rb.WakeUp();
                        else if (!rb.isKinematic && !rb.IsSleeping()) rb.Sleep();
                    }
                    catch (System.Exception e)
                    {
                        if (Plugin.DebugLoggingEnabled.Value)
                            Plugin.Log.LogWarning(
                                $"[DistanceCuller] Rigidbody error: {e.Message}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                // Отписываемся от менеджера.
                DistanceCullerManager.Instance?.UnregisterCuller(this);

                if (_isCulled)
                    SetComponentsEnabled(true);

                _culledComponents.Clear();
                _trackedRigidbodies.Clear();
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogError($"[DistanceCuller] OnDestroy error: {e.Message}");
            }
        }
    }
}
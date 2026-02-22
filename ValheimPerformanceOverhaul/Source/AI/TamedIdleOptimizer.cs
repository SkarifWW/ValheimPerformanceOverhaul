using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul.AI
{
                    public class TamedIdleOptimizer : MonoBehaviour
    {
                private Character _character;
        private BaseAI _baseAI;
        private MonsterAI _monsterAI;
        private ZNetView _nview;
        private Animator _animator;
        private Rigidbody _rigidbody;
        private Collider[] _colliders;

                private bool _isInIdleMode = false;
        private float _lastStateChange = 0f;
        private float _lastCombatTime = 0f;
        private float _lastCheckTime = 0f;

                private bool _originalAIEnabled = true;
        private float _originalAnimatorSpeed = 1f;
        private bool _originalRigidbodyKinematic = false;
        private bool[] _originalColliderStates;

                private const float CHECK_INTERVAL = 1f;         private const float MIN_TIME_IN_COMBAT = 5f;         private const float STATE_CHANGE_COOLDOWN = 2f;         private const float BASE_DETECTION_RADIUS = 30f; 
        private void Awake()
        {
            _character = GetComponent<Character>();
            _baseAI = GetComponent<BaseAI>();
            _monsterAI = GetComponent<MonsterAI>();
            _nview = GetComponent<ZNetView>();
            _animator = GetComponentInChildren<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();

                        if (_animator != null)
            {
                _originalAnimatorSpeed = _animator.speed;
            }

            if (_rigidbody != null)
            {
                _originalRigidbodyKinematic = _rigidbody.isKinematic;
            }

                        if (_colliders != null && _colliders.Length > 0)
            {
                _originalColliderStates = new bool[_colliders.Length];
                for (int i = 0; i < _colliders.Length; i++)
                {
                    _originalColliderStates[i] = _colliders[i] != null ? _colliders[i].enabled : false;
                }
            }

            _lastCheckTime = Time.time;
        }

        private void Update()
        {
                        if (ZNet.instance == null || !ZNet.instance.IsServer())
                return;

                        if (!Plugin.TamedIdleOptimizationEnabled.Value)
            {
                                if (_isInIdleMode)
                {
                    ExitIdleMode();
                }
                return;
            }

                        float currentTime = Time.time;
            if (currentTime - _lastCheckTime < CHECK_INTERVAL)
                return;

            _lastCheckTime = currentTime;

                        if (_character != null && _character.InAttack())
            {
                _lastCombatTime = currentTime;
            }

                        if (_isInIdleMode)
            {
                if (ShouldExitIdle())
                {
                    ExitIdleMode();
                }
            }
            else
            {
                if (ShouldEnterIdle())
                {
                    EnterIdleMode();
                }
            }
        }

                                private bool ShouldEnterIdle()
        {
                        if (Time.time - _lastStateChange < STATE_CHANGE_COOLDOWN)
                return false;

                        if (_baseAI != null && _baseAI.GetTargetCreature() != null)
                return false;

                        return IsTamed()
                && !IsInCombat()
                && IsInsideBase()
                && !HasFollowTarget()
                && (Time.time - _lastCombatTime > Plugin.TamedIdleDistanceFromCombat.Value);
        }

                                private bool ShouldExitIdle()
        {
                        return IsInCombat()
                || HasFollowTarget()
                || PlayerInteracted()
                || !IsInsideBase();
        }

                                private bool IsTamed()
        {
            if (_character == null) return false;
            return _character.IsTamed();
        }

                                private bool IsInCombat()
        {
            if (_character == null) return false;
            
                                    if (_character.InAttack()) return true;
            
                        if (_character.GetHealth() < _character.GetMaxHealth())
            {
                                            }
            
                        if (_baseAI != null)
            {
                                if (_baseAI.IsAlerted()) return true;
                
                                if (_baseAI.GetTargetCreature() != null) return true;
                
                                if (_baseAI.HaveTarget()) return true;
            }
            
                        if (_monsterAI != null)
            {
                                var targetCreature = _monsterAI.GetTargetCreature();
                if (targetCreature != null && !targetCreature.IsDead())
                    return true;
            }
            
            return false;
        }

                                private bool IsInsideBase()
        {
                        bool insidePrivateArea = PrivateArea.CheckAccess(transform.position, 0f, false, false);
            if (insidePrivateArea)
                return true;

                        bool nearPlayerPieces = false;
            Collider[] colliders = Physics.OverlapSphere(transform.position, Plugin.TamedIdleBaseDetectionRadius.Value);
            foreach (var col in colliders)
            {
                Piece piece = col.GetComponentInParent<Piece>();
                if (piece != null)
                {
                    nearPlayerPieces = true;
                    break;
                }
            }

            return nearPlayerPieces;
        }

                                private bool HasFollowTarget()
        {
            if (_monsterAI == null) return false;
            
                        GameObject followTargetGO = _monsterAI.GetFollowTarget();
            return followTargetGO != null;
        }

                                private bool PlayerInteracted()
        {
                        if (Player.m_localPlayer != null)
            {
                float distSqr = (Player.m_localPlayer.transform.position - transform.position).sqrMagnitude;
                if (distSqr < 25f)                 {
                    return true;
                }
            }

            return false;
        }

                                private void EnterIdleMode()
        {
            if (_isInIdleMode) return;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[TamedIdleOptimizer] {gameObject.name} entering IDLE MODE");
            }

                        if (_baseAI != null)
            {
                _originalAIEnabled = _baseAI.enabled;
                _baseAI.enabled = false;
                
                                _baseAI.StopMoving();
                
                                if (_monsterAI != null)
                {
                    _monsterAI.SetFollowTarget(null);
                }
            }

                        if (_animator != null)
            {
                _originalAnimatorSpeed = _animator.speed;
                _animator.speed = 0f;
                _animator.enabled = false;
            }

                        
            _isInIdleMode = true;
            _lastStateChange = Time.time;
        }

                                private void ExitIdleMode()
        {
            if (!_isInIdleMode) return;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[TamedIdleOptimizer] {gameObject.name} exiting IDLE MODE");
            }

                        if (_baseAI != null)
            {
                _baseAI.enabled = _originalAIEnabled;
            }

                        if (_animator != null)
            {
                _animator.enabled = true;
                _animator.speed = _originalAnimatorSpeed;
            }

            
            _isInIdleMode = false;
            _lastStateChange = Time.time;
        }

                                public bool IsInIdleMode()
        {
            return _isInIdleMode;
        }

        private void OnDestroy()
        {
                        if (_isInIdleMode)
            {
                ExitIdleMode();
            }
        }
    }
}

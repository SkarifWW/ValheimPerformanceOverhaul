using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace ValheimPerformanceOverhaul.AI
{
    public class AIOptimizer : MonoBehaviour
    {
        private BaseAI _ai;
        private ZNetView _nview;
        private Character _character;
        
        private float _distCheckTimer;
        private const float DIST_CHECK_INTERVAL = 1.0f;
        private float _closestPlayerDistSqr = 1000000f;

                private Character _cachedEnemy;
        private float _lastEnemyCheck;

        private float _lastAttackCheck;
        
                private float _lastPathUpdate;
        private Vector3 _lastPathTargetPos;

        private readonly Dictionary<int, (bool visible, float time)> _losCache = new Dictionary<int, (bool, float)>();

                private const int MAX_LOS_CACHE_SIZE = 100;
        private const float LOS_CACHE_TIMEOUT = 5f;
        private float _losCleanupTimer = 0f;

                private Rigidbody _rigidbody;
        private Collider[] _colliders;
        private bool _physicsActive = true;

                private Animator _animator;
        private bool _animatorActive = true;
        private float _originalAnimatorSpeed = 1f;

        private void Awake()
        {
            _ai = GetComponent<BaseAI>();
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
            
                        _rigidbody = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
            _animator = GetComponentInChildren<Animator>();
            
            if (_animator != null)
            {
                _originalAnimatorSpeed = _animator.speed;
            }
        }

        private void OnEnable()
        {
                        _closestPlayerDistSqr = 0f;             _distCheckTimer = DIST_CHECK_INTERVAL;             
            _cachedEnemy = null;
            _lastEnemyCheck = -100f;
            _lastAttackCheck = -100f;
            _lastPathUpdate = -100f;
            _lastPathTargetPos = Vector3.zero;
            
            _losCache.Clear();
            _losCleanupTimer = 0f;

            _physicsActive = true;
            _animatorActive = true;
            
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.speed = _originalAnimatorSpeed;
            }
            
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                _rigidbody.WakeUp();
            }
        }

        public float GetDistanceToPlayer() => Mathf.Sqrt(_closestPlayerDistSqr);

        private void FixedUpdate()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;

            _distCheckTimer += Time.fixedDeltaTime;
            if (_distCheckTimer < DIST_CHECK_INTERVAL) return;
            _distCheckTimer = 0f;

            UpdateClosestPlayerDistance();
            UpdatePhysicsAndAnimator();

                        _losCleanupTimer += DIST_CHECK_INTERVAL;
            if (_losCleanupTimer >= 10f)             {
                _losCleanupTimer = 0f;
                CleanupLOSCache();
            }
        }

        private void UpdateClosestPlayerDistance()
        {
            _closestPlayerDistSqr = 1000000f;
            
                        if (Player.m_localPlayer != null)
            {
                _closestPlayerDistSqr = (Player.m_localPlayer.transform.position - transform.position).sqrMagnitude;
            }

                        var players = Player.GetAllPlayers();
            foreach (var player in players)
            {
                if (player == null) continue;
                float distSqr = (player.transform.position - transform.position).sqrMagnitude;
                if (distSqr < _closestPlayerDistSqr)
                    _closestPlayerDistSqr = distSqr;
            }
        }

                private void UpdatePhysicsAndAnimator()
        {
            if (!Plugin.AiThrottlingEnabled.Value) return;

            float dist = GetDistanceToPlayer();

                        if (Plugin.CullPhysicsEnabled.Value && _rigidbody != null)
            {
                bool shouldBeActive = dist < 120f;                 
                if (shouldBeActive != _physicsActive)
                {
                    SetPhysicsActive(shouldBeActive);
                }
            }

                        if (Plugin.AnimatorOptimizationEnabled.Value && _animator != null)
            {
                if (dist > 120f)                 {
                    if (_animatorActive)
                    {
                        _animator.enabled = false;
                        _animatorActive = false;
                    }
                }
                else if (dist > 60f)                 {
                    if (!_animatorActive)
                    {
                        _animator.enabled = true;
                        _animatorActive = true;
                    }
                    _animator.speed = _originalAnimatorSpeed * 0.5f;                 }
                else                 {
                    if (!_animatorActive)
                    {
                        _animator.enabled = true;
                        _animatorActive = true;
                    }
                    _animator.speed = _originalAnimatorSpeed;
                }
            }
        }

        private void SetPhysicsActive(bool active)
        {
                                    if (_character != null) return;

            if (_rigidbody != null)
            {
                 if (!active)
                 {
                     if (!_rigidbody.isKinematic)
                     {
                         _rigidbody.velocity = Vector3.zero;
                         _rigidbody.angularVelocity = Vector3.zero;
                     }
                 }
                 _rigidbody.isKinematic = !active;
            }

            if (_colliders != null)
            {
                foreach (var collider in _colliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = active;
                    }
                }
            }
            
            _physicsActive = active;
        }

        
        public bool ShouldCheckEnemy(out Character currentEnemy)
        {
            currentEnemy = _cachedEnemy;
            float currentTime = Time.time;
            float interval = GetInterval(0.5f, 2.0f);

            if (currentTime - _lastEnemyCheck >= interval)
            {
                _lastEnemyCheck = currentTime;
                return true;
            }

            return false;
        }

        public void UpdateCachedEnemy(Character enemy) => _cachedEnemy = enemy;

        public bool ShouldCheckAttack()
        {
            float currentTime = Time.time;
            float interval = GetInterval(0.3f, 1.0f);

            if (currentTime - _lastAttackCheck >= interval)
            {
                _lastAttackCheck = currentTime;
                return true;
            }
            return false;
        }

        public bool ShouldUpdatePath(Vector3 targetPos)
        {
            float currentTime = Time.time;
            float interval = GetInterval(0.5f, 2.0f);

            bool intervalPassed = (currentTime - _lastPathUpdate >= interval);
            bool targetMoved = ((targetPos - _lastPathTargetPos).sqrMagnitude >= 4f); 
                                                if (intervalPassed || targetMoved)
            {
                _lastPathUpdate = currentTime;
                _lastPathTargetPos = targetPos;
                return true;
            }
            
            return false;         }

        public bool GetCachedLOS(Character target, out bool visible)
        {
            visible = false;
            if (target == null) return false;

            int targetId = target.GetHashCode();
            float currentTime = Time.time;

            if (_losCache.TryGetValue(targetId, out var entry))
            {
                if (currentTime - entry.time < 0.5f)
                {
                    visible = entry.visible;
                    return true;
                }
            }
            return false;
        }

        public void SetCachedLOS(Character target, bool visible)
        {
            if (target == null) return;
            _losCache[target.GetHashCode()] = (visible, Time.time);
        }

        private float GetInterval(float min, float max)
        {
            if (_closestPlayerDistSqr < 40f * 40f) return min;
            if (_closestPlayerDistSqr > 120f * 120f) return max;
            
            float t = (_closestPlayerDistSqr - 1600f) / (14400f - 1600f);
            return Mathf.Lerp(min, max, t);
        }

                        private void CleanupLOSCache()
        {
            if (Time.frameCount % 600 != 0) return; 
            float currentTime = Time.time;
            var deadKeys = new List<int>();

            foreach (var kvp in _losCache)
            {
                if (currentTime - kvp.Value.time > 10f)                 {
                    deadKeys.Add(kvp.Key);
                }
            }

            foreach (var key in deadKeys)
            {
                _losCache.Remove(key);
            }
        }

        private void OnDestroy()
        {
                        _losCache.Clear();
        }
    }
}

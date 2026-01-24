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

        // Caching State
        private Character _cachedEnemy;
        private float _lastEnemyCheck;

        private float _lastAttackCheck;
        
        // Pathfinding cache
        private float _lastPathUpdate;
        private Vector3 _lastPathTargetPos;

        private readonly Dictionary<int, (bool visible, float time)> _losCache = new Dictionary<int, (bool, float)>();

        // === МОДУЛЬ 5: Physics Sleep ===
        private Rigidbody _rigidbody;
        private Collider[] _colliders;
        private bool _physicsActive = true;

        // === МОДУЛЬ 7: Animator Optimization ===
        private Animator _animator;
        private bool _animatorActive = true;
        private float _originalAnimatorSpeed = 1f;

        private void Awake()
        {
            _ai = GetComponent<BaseAI>();
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
            _distCheckTimer = Random.Range(0f, DIST_CHECK_INTERVAL);

            // Получаем компоненты для оптимизации
            _rigidbody = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
            _animator = GetComponentInChildren<Animator>();
            
            if (_animator != null)
            {
                _originalAnimatorSpeed = _animator.speed;
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
        }

        private void UpdateClosestPlayerDistance()
        {
            _closestPlayerDistSqr = 1000000f;
            
            // Try local player first (most efficient)
            if (Player.m_localPlayer != null)
            {
                _closestPlayerDistSqr = (Player.m_localPlayer.transform.position - transform.position).sqrMagnitude;
            }

            // Check other players if needed
            var players = Player.GetAllPlayers();
            foreach (var player in players)
            {
                if (player == null) continue;
                float distSqr = (player.transform.position - transform.position).sqrMagnitude;
                if (distSqr < _closestPlayerDistSqr)
                    _closestPlayerDistSqr = distSqr;
            }
        }

        // === МОДУЛЬ 5 & 7: Physics and Animator Management ===
        private void UpdatePhysicsAndAnimator()
        {
            if (!Plugin.AiThrottlingEnabled.Value) return;

            float dist = GetDistanceToPlayer();

            // МОДУЛЬ 5: Physics Sleep
            if (Plugin.CullPhysicsEnabled.Value && _rigidbody != null)
            {
                bool shouldBeActive = dist < 120f; // Радиус активации физики
                
                if (shouldBeActive != _physicsActive)
                {
                    SetPhysicsActive(shouldBeActive);
                }
            }

            // МОДУЛЬ 7: Animator Optimization
            if (Plugin.AnimatorOptimizationEnabled.Value && _animator != null)
            {
                if (dist > 120f) // Очень далеко - выключаем
                {
                    if (_animatorActive)
                    {
                        _animator.enabled = false;
                        _animatorActive = false;
                    }
                }
                else if (dist > 60f) // Средняя дистанция - замедляем
                {
                    if (!_animatorActive)
                    {
                        _animator.enabled = true;
                        _animatorActive = true;
                    }
                    _animator.speed = _originalAnimatorSpeed * 0.5f; // 50% скорости анимации
                }
                else // Близко - полная скорость
                {
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
            if (_rigidbody != null)
            {
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

        // --- Throttling Logic ---

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
            bool targetMoved = ((targetPos - _lastPathTargetPos).sqrMagnitude >= 4f); // 2 метра

            // Обновляем путь если:
            // 1. Прошёл интервал времени ИЛИ
            // 2. Цель сместилась достаточно далеко
            if (intervalPassed || targetMoved)
            {
                _lastPathUpdate = currentTime;
                _lastPathTargetPos = targetPos;
                return true;
            }
            
            return false; // Пропускаем обновление - используем старый путь
        }

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
    }
}

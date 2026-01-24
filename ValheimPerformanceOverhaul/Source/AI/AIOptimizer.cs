using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace ValheimPerformanceOverhaul.AI
{
    public class AIOptimizer : MonoBehaviour
    {
        private BaseAI _ai;
        private ZNetView _nview;
        
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

        private void Awake()
        {
            _ai = GetComponent<BaseAI>();
            _nview = GetComponent<ZNetView>();
            _distCheckTimer = Random.Range(0f, DIST_CHECK_INTERVAL);
        }

        public float GetDistanceToPlayer() => Mathf.Sqrt(_closestPlayerDistSqr);

        private void FixedUpdate()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;

            _distCheckTimer += Time.fixedDeltaTime;
            if (_distCheckTimer < DIST_CHECK_INTERVAL) return;
            _distCheckTimer = 0f;

            UpdateClosestPlayerDistance();
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

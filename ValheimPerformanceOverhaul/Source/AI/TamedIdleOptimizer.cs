using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul.AI
{
    /// <summary>
    /// BASE IDLE MODE оптимизация для прирученных мобов
    /// Переводит прирученных мобов на базе в статический режим для радикального снижения нагрузки
    /// </summary>
    public class TamedIdleOptimizer : MonoBehaviour
    {
        // === КОМПОНЕНТЫ ===
        private Character _character;
        private BaseAI _baseAI;
        private MonsterAI _monsterAI;
        private ZNetView _nview;
        private Animator _animator;
        private Rigidbody _rigidbody;
        private Collider[] _colliders;

        // === СОСТОЯНИЕ IDLE MODE ===
        private bool _isInIdleMode = false;
        private float _lastStateChange = 0f;
        private float _lastCombatTime = 0f;
        private float _lastCheckTime = 0f;

        // === СОХРАНЕННЫЕ СОСТОЯНИЯ ===
        private bool _originalAIEnabled = true;
        private float _originalAnimatorSpeed = 1f;
        private bool _originalRigidbodyKinematic = false;
        private bool[] _originalColliderStates;

        // === КОНСТАНТЫ ===
        private const float CHECK_INTERVAL = 1f; // Проверка раз в секунду
        private const float MIN_TIME_IN_COMBAT = 5f; // Минимум 5 секунд после боя
        private const float STATE_CHANGE_COOLDOWN = 2f; // Кулдаун между сменами состояния
        private const float BASE_DETECTION_RADIUS = 30f; // Радиус определения базы

        private void Awake()
        {
            _character = GetComponent<Character>();
            _baseAI = GetComponent<BaseAI>();
            _monsterAI = GetComponent<MonsterAI>();
            _nview = GetComponent<ZNetView>();
            _animator = GetComponentInChildren<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();

            // Сохраняем исходные состояния
            if (_animator != null)
            {
                _originalAnimatorSpeed = _animator.speed;
            }

            if (_rigidbody != null)
            {
                _originalRigidbodyKinematic = _rigidbody.isKinematic;
            }

            // Сохраняем состояния коллайдеров
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
            // ❌ Работает ТОЛЬКО на сервере
            if (ZNet.instance == null || !ZNet.instance.IsServer())
                return;

            // Проверяем только если включена оптимизация
            if (!Plugin.TamedIdleOptimizationEnabled.Value)
            {
                // Если оптимизация выключена, но моб в Idle Mode - восстанавливаем
                if (_isInIdleMode)
                {
                    ExitIdleMode();
                }
                return;
            }

            // Проверка с интервалом
            float currentTime = Time.time;
            if (currentTime - _lastCheckTime < CHECK_INTERVAL)
                return;

            _lastCheckTime = currentTime;

            // Обновляем время последнего боя
            if (_character != null && _character.InAttack())
            {
                _lastCombatTime = currentTime;
            }

            // Принимаем решение о входе/выходе из Idle Mode
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

        /// <summary>
        /// Проверка условий для ВХОДА в Idle Mode
        /// </summary>
        private bool ShouldEnterIdle()
        {
            // Кулдаун между сменами состояния
            if (Time.time - _lastStateChange < STATE_CHANGE_COOLDOWN)
                return false;

            // Проверяем все обязательные условия
            return IsTamed()
                && !IsInCombat()
                && IsInsideBase()
                && !HasFollowTarget()
                && (Time.time - _lastCombatTime > Plugin.TamedIdleDistanceFromCombat.Value);
        }

        /// <summary>
        /// Проверка условий для ВЫХОДА из Idle Mode
        /// </summary>
        private bool ShouldExitIdle()
        {
            // Немедленный выход при любом из условий
            return IsInCombat()
                || HasFollowTarget()
                || PlayerInteracted()
                || !IsInsideBase();
        }

        /// <summary>
        /// Прирученный ли моб?
        /// </summary>
        private bool IsTamed()
        {
            if (_character == null) return false;
            return _character.IsTamed();
        }

        /// <summary>
        /// В бою ли моб?
        /// </summary>
        private bool IsInCombat()
        {
            if (_character == null) return false;
            
            // Проверяем несколько признаков боя
            return _character.InAttack() 
                || (_baseAI != null && _baseAI.IsAlerted())
                || (_baseAI != null && _baseAI.GetTargetCreature() != null);
        }

        /// <summary>
        /// Находится ли моб на базе?
        /// </summary>
        private bool IsInsideBase()
        {
            // Проверяем наличие PrivateArea поблизости
            bool insidePrivateArea = PrivateArea.CheckAccess(transform.position, 0f, false, false);
            if (insidePrivateArea)
                return true;

            // Альтернативно: проверяем наличие построек игрока
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

        /// <summary>
        /// Следует ли моб за игроком?
        /// </summary>
        private bool HasFollowTarget()
        {
            if (_monsterAI == null) return false;
            
            // Проверяем, есть ли у MonsterAI цель следования
            GameObject followTargetGO = _monsterAI.GetFollowTarget();
            return followTargetGO != null;
        }

        /// <summary>
        /// Взаимодействовал ли игрок с мобом недавно?
        /// </summary>
        private bool PlayerInteracted()
        {
            // Проверяем близость игрока (менее 5 метров)
            if (Player.m_localPlayer != null)
            {
                float distSqr = (Player.m_localPlayer.transform.position - transform.position).sqrMagnitude;
                if (distSqr < 25f) // 5 метров
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// ВХОД в Idle Mode - выключение всех систем
        /// </summary>
        private void EnterIdleMode()
        {
            if (_isInIdleMode) return;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[TamedIdleOptimizer] {gameObject.name} entering IDLE MODE");
            }

            // 1️⃣ ВЫКЛЮЧАЕМ AI
            if (_baseAI != null)
            {
                _originalAIEnabled = _baseAI.enabled;
                _baseAI.enabled = false;
                
                // Останавливаем движение
                _baseAI.StopMoving();
                
                // Убираем цель следования
                if (_monsterAI != null)
                {
                    _monsterAI.SetFollowTarget(null);
                }
            }

            // 2️⃣ ВЫКЛЮЧАЕМ ANIMATOR
            if (_animator != null)
            {
                _originalAnimatorSpeed = _animator.speed;
                _animator.speed = 0f;
                _animator.enabled = false;
            }

            // 3️⃣ МИНИМИЗИРУЕМ ФИЗИКУ
            if (_rigidbody != null)
            {
                _originalRigidbodyKinematic = _rigidbody.isKinematic;
                _rigidbody.isKinematic = true;
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            // 4️⃣ ОТКЛЮЧАЕМ КОЛЛАЙДЕРЫ (опционально, для максимальной производительности)
            // Осторожно: это может привести к провалам через текстуры
            /*
            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null)
                    {
                        _colliders[i].enabled = false;
                    }
                }
            }
            */

            _isInIdleMode = true;
            _lastStateChange = Time.time;
        }

        /// <summary>
        /// ВЫХОД из Idle Mode - восстановление всех систем
        /// </summary>
        private void ExitIdleMode()
        {
            if (!_isInIdleMode) return;

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[TamedIdleOptimizer] {gameObject.name} exiting IDLE MODE");
            }

            // 1️⃣ ВОССТАНАВЛИВАЕМ AI
            if (_baseAI != null)
            {
                _baseAI.enabled = _originalAIEnabled;
            }

            // 2️⃣ ВОССТАНАВЛИВАЕМ ANIMATOR
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.speed = _originalAnimatorSpeed;
            }

            // 3️⃣ ВОССТАНАВЛИВАЕМ ФИЗИКУ
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = _originalRigidbodyKinematic;
            }

            // 4️⃣ ВОССТАНАВЛИВАЕМ КОЛЛАЙДЕРЫ
            if (_colliders != null && _originalColliderStates != null)
            {
                for (int i = 0; i < _colliders.Length && i < _originalColliderStates.Length; i++)
                {
                    if (_colliders[i] != null)
                    {
                        _colliders[i].enabled = _originalColliderStates[i];
                    }
                }
            }

            _isInIdleMode = false;
            _lastStateChange = Time.time;
        }

        /// <summary>
        /// Получить текущее состояние Idle Mode (для отладки)
        /// </summary>
        public bool IsInIdleMode()
        {
            return _isInIdleMode;
        }

        private void OnDestroy()
        {
            // При уничтожении объекта - всегда восстанавливаем состояние
            if (_isInIdleMode)
            {
                ExitIdleMode();
            }
        }
    }
}

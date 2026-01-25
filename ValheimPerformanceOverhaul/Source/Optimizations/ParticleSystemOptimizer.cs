using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.Particles
{
    public class TrackedParticle : MonoBehaviour
    {
        public ParticleSystem System { get; private set; }
        private bool _wasPlaying = false;
        private bool _isCulled = false;

        private void Awake()
        {
            System = GetComponent<ParticleSystem>();
        }

        public void SetCulled(bool culled)
        {
            if (System == null || _isCulled == culled) return;

            if (culled)
            {
                _wasPlaying = System.isPlaying;
                if (_wasPlaying)
                {
                    System.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            else
            {
                if (_wasPlaying && !System.isPlaying)
                {
                    System.Play(true);
                }
            }

            _isCulled = culled;
        }

        public bool IsCulled => _isCulled;
    }

    public class ParticleSystemManager : MonoBehaviour
    {
        public static ParticleSystemManager Instance { get; private set; }

        private readonly List<TrackedParticle> _allParticles = new List<TrackedParticle>(1024);
        private readonly HashSet<TrackedParticle> _culledParticles = new HashSet<TrackedParticle>();

        private float _updateTimer;
        private const float UPDATE_INTERVAL = 0.5f;
        private float _scanTimer;
        private const float SCAN_INTERVAL = 30f;

        private float _cullDistance = 50f;
        private int _maxActiveParticles = 30;

        // ✅ НОВОЕ: Кэшированный comparer для уменьшения GC
        private static Vector3 _cameraPos;
        private static readonly System.Comparison<TrackedParticle> _particleComparer = (a, b) =>
        {
            float distA = (a.transform.position - _cameraPos).sqrMagnitude;
            float distB = (b.transform.position - _cameraPos).sqrMagnitude;
            return distA.CompareTo(distB);
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            LoadConfig();
            ScanForParticles();
            Plugin.Log.LogInfo($"[ParticleOptimization] Initialized with {_allParticles.Count} particle systems.");
        }

        private void LoadConfig()
        {
            _cullDistance = Plugin.ParticleCullDistance?.Value ?? 50f;
            _maxActiveParticles = Plugin.MaxActiveParticles?.Value ?? 30;
        }

        private void ScanForParticles()
        {
            ParticleSystem[] particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ps in particles)
            {
                RegisterParticleSystem(ps);
            }
        }

        public void RegisterParticleSystem(ParticleSystem ps)
        {
            if (ps == null || ps.GetComponent<TrackedParticle>() != null) return;

            // ✅ ИСПРАВЛЕНО: Быстрая проверка по имени СНАЧАЛА
            string goName = ps.gameObject.name.ToLower();
            string rootName = ps.transform.root.name.ToLower();

            // Быстрая проверка по имени (без GetComponent)
            if (goName.Contains("ship") || goName.Contains("vehicle") ||
                goName.Contains("boat") || goName.Contains("karve") ||
                rootName.Contains("ship") || rootName.Contains("vehicle") ||
                rootName.Contains("boat") || rootName.Contains("karve"))
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ParticleOptimization] Excluded by name: {ps.name}");
                return;
            }

            // Пропускаем UI партиклы (быстрая проверка через layer)
            if (ps.gameObject.layer == LayerMask.NameToLayer("UI"))
                return;

            // ✅ ИСПРАВЛЕНО: Более целевая проверка компонентов
            // ТОЛЬКО если быстрая проверка не сработала
            var shipComponent = ps.GetComponentInParent<Ship>();
            if (shipComponent != null)
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ParticleOptimization] Excluded ship particle: {ps.name}");
                return;
            }

            // ✅ НОВОЕ: Проверяем ValheimVehicles через имя типа
            // (избегаем hard reference на мод)
            var parentRb = ps.GetComponentInParent<Rigidbody>();
            if (parentRb != null && parentRb.gameObject.name.ToLower().Contains("vehicle"))
            {
                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo($"[ParticleOptimization] Excluded vehicle particle: {ps.name}");
                return;
            }

            var tracked = ps.gameObject.AddComponent<TrackedParticle>();
            _allParticles.Add(tracked);
        }

        private void Update()
        {
            if (!Plugin.ParticleOptimizationEnabled.Value || Player.m_localPlayer == null)
            {
                return;
            }

            _updateTimer += Time.deltaTime;
            _scanTimer += Time.deltaTime;

            if (_scanTimer >= SCAN_INTERVAL)
            {
                _scanTimer = 0f;
                ScanForParticles();
            }

            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            UpdateParticleCulling();
        }

        private void UpdateParticleCulling()
        {
            // ✅ ИСПРАВЛЕНО: Безопасное удаление мертвых ссылок
            for (int i = _allParticles.Count - 1; i >= 0; i--)
            {
                if (_allParticles[i] == null || _allParticles[i].System == null)
                {
                    _allParticles.RemoveAt(i);
                }
            }

            if (_allParticles.Count == 0) return;

            // ✅ ИСПРАВЛЕНО: Используем кэшированный comparer
            _cameraPos = Camera.main.transform.position;
            float cullDistSqr = _cullDistance * _cullDistance;

            // Сортируем с использованием статического comparer (без lambda allocation)
            _allParticles.Sort(_particleComparer);

            int activeCount = 0;

            foreach (var particle in _allParticles)
            {
                if (particle == null || particle.System == null) continue;

                float distSqr = (particle.transform.position - _cameraPos).sqrMagnitude;
                bool shouldBeActive = distSqr <= cullDistSqr && activeCount < _maxActiveParticles;

                if (shouldBeActive)
                {
                    if (particle.IsCulled)
                    {
                        particle.SetCulled(false);
                        _culledParticles.Remove(particle);
                    }
                    activeCount++;
                }
                else
                {
                    if (!particle.IsCulled)
                    {
                        particle.SetCulled(true);
                        _culledParticles.Add(particle);
                    }
                }
            }

            if (Plugin.DebugLoggingEnabled.Value && Time.frameCount % 240 == 0)
            {
                Plugin.Log.LogInfo($"[ParticleOptimization] Active: {activeCount}/{_maxActiveParticles}, Culled: {_culledParticles.Count}, Total: {_allParticles.Count}");
            }
        }

        private void OnDestroy()
        {
            foreach (var particle in _culledParticles)
            {
                if (particle != null)
                {
                    particle.SetCulled(false);
                }
            }
            Instance = null;
        }
    }

    [HarmonyPatch]
    public static class ParticlePatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void InitializeParticleManager(Player __instance)
        {
            if (!Plugin.ParticleOptimizationEnabled.Value || __instance != Player.m_localPlayer)
            {
                return;
            }

            if (ParticleSystemManager.Instance != null) return;

            var manager = new GameObject("_VPO_ParticleSystemManager");
            Object.DontDestroyOnLoad(manager);
            manager.AddComponent<ParticleSystemManager>();
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPostfix]
        private static void RegisterNewParticles(GameObject __result)
        {
            if (!Plugin.ParticleOptimizationEnabled.Value || ParticleSystemManager.Instance == null || __result == null)
            {
                return;
            }

            var particles = __result.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                ParticleSystemManager.Instance.RegisterParticleSystem(ps);
            }
        }
    }
}
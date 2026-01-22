using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
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
        private const float SCAN_INTERVAL = 10f;

        private float _cullDistance = 50f;
        private int _maxActiveParticles = 30;

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
            ParticleSystem[] particles = FindObjectsOfType<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                RegisterParticleSystem(ps);
            }
        }

        public void RegisterParticleSystem(ParticleSystem ps)
        {
            if (ps == null || ps.GetComponent<TrackedParticle>() != null) return;

            // Пропускаем UI партиклы
            if (ps.GetComponentInParent<Canvas>() != null) return;

            // === ФИКС ===
            // Пропускаем партиклы кораблей (ванильных) и ValheimVehicles
            // Проверяем компоненты в родителях
            var parentComponents = ps.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var comp in parentComponents)
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;

                // ShipEffects - ванильный, VehicleShipEffects - из мода
                if (typeName == "Ship" || typeName == "ShipEffects" ||
                    typeName == "VehicleShipEffects" || typeName == "VehicleController")
                {
                    return; // Не трогаем этот партикл
                }
            }
            // ============

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
            _allParticles.RemoveAll(p => p == null || p.System == null);
            if (_allParticles.Count == 0) return;

            Vector3 cameraPos = Camera.main.transform.position;
            float cullDistSqr = _cullDistance * _cullDistance;

            // Сортируем по расстоянию
            _allParticles.Sort((a, b) =>
            {
                float distA = (a.transform.position - cameraPos).sqrMagnitude;
                float distB = (b.transform.position - cameraPos).sqrMagnitude;
                return distA.CompareTo(distB);
            });

            int activeCount = 0;

            foreach (var particle in _allParticles)
            {
                if (particle == null || particle.System == null) continue;

                float distSqr = (particle.transform.position - cameraPos).sqrMagnitude;
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
                Plugin.Log.LogInfo($"[ParticleOptimization] Active: {activeCount}/{_maxActiveParticles}, Culled: {_culledParticles.Count}");
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
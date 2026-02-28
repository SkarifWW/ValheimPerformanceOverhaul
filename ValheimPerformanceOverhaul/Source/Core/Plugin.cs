using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using ValheimPerformanceOverhaul.AI;
using ValheimPerformanceOverhaul.Audio;
using ValheimPerformanceOverhaul.Core;
using ValheimPerformanceOverhaul.LightCulling;
using ValheimPerformanceOverhaul.ObjectPooling;

namespace ValheimPerformanceOverhaul
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGUID = "com.Skarif.ValheimPerformanceOverhaul";
        private const string PluginName = "Valheim Performance Overhaul";
        private const string PluginVersion = "2.7.0";

        private readonly Harmony _harmony = new Harmony(PluginGUID);

        public static ManualLogSource Log;
        public static Plugin Instance;

        public static ConfigEntry<bool> DebugLoggingEnabled;

        public static ConfigEntry<bool> GcControlEnabled;

        public static ConfigEntry<bool> DistanceCullerEnabled;
        public static ConfigEntry<float> CreatureCullDistance;
        public static ConfigEntry<float> PieceCullDistance;
        public static ConfigEntry<bool> CullPhysicsEnabled;
        public static ConfigEntry<bool> AiThrottlingEnabled;
        public static ConfigEntry<string> CullerExclusions;

        public static ConfigEntry<bool> ObjectPoolingEnabled;

        public static ConfigEntry<bool> JitWarmupEnabled;

        public static ConfigEntry<bool> LightCullingEnabled;
        public static ConfigEntry<int> MaxActiveLights;
        public static ConfigEntry<float> LightCullDistance;
        public static ConfigEntry<int> MaxShadowCasters;
        public static ConfigEntry<float> ShadowCullDistance;
        public static ConfigEntry<bool> LightLODEnabled;
        public static ConfigEntry<float> LightLODFullDistance;
        public static ConfigEntry<float> LightLODNoShadowDistance;
        public static ConfigEntry<float> LightLODEmissiveDistance;
        public static ConfigEntry<float> LightLODBillboardDistance;

        public static ConfigEntry<bool> AudioPoolingEnabled;
        public static ConfigEntry<int> AudioPoolSize;

        public static ConfigEntry<bool> GraphicsSettingsEnabled;
        public static ConfigEntry<float> ConfigShadowDistance;
        public static ConfigEntry<int> ConfigShadowResolution;
        public static ConfigEntry<int> ConfigShadowCascades;
        public static ConfigEntry<float> ConfigTerrainQuality;
        public static ConfigEntry<bool> ConfigReflections;
        public static ConfigEntry<bool> ConfigBloom;

        public static ConfigEntry<bool> PieceOptimizationEnabled;
        public static ConfigEntry<float> PieceUpdateInterval;
        public static ConfigEntry<float> PieceColliderDistance;
        public static ConfigEntry<float> PieceSupportCacheDuration;
        public static ConfigEntry<float> PieceUpdateSkipDistance;

        public static ConfigEntry<bool> ParticleOptimizationEnabled;
        public static ConfigEntry<float> ParticleCullDistance;
        public static ConfigEntry<int> MaxActiveParticles;

        public static ConfigEntry<bool> VegetationOptimizationEnabled;
        public static ConfigEntry<float> GrassRenderDistance;
        public static ConfigEntry<float> GrassDensityMultiplier;
        public static ConfigEntry<float> DetailObjectDistance;
        public static ConfigEntry<float> DetailDensity;
        public static ConfigEntry<int> TerrainMaxLOD;

        public static ConfigEntry<bool> AnimatorOptimizationEnabled;

        public static ConfigEntry<bool> MinimapOptimizationEnabled;
        public static ConfigEntry<int> MinimapTextureSize;
        public static ConfigEntry<int> MinimapUpdateInterval;

        public static ConfigEntry<bool> TamedIdleOptimizationEnabled;
        public static ConfigEntry<float> TamedIdleDistanceFromCombat;
        public static ConfigEntry<float> TamedIdleBaseDetectionRadius;
        public static ConfigEntry<float> TamedIdleCheckInterval;


        public static ConfigEntry<bool> LightFlickerOptimizationEnabled;

        public static ConfigEntry<bool> SmokeOptimizationEnabled;
        public static ConfigEntry<float> SmokeLiftForce;

        public static ConfigEntry<bool> EngineQualitySettingsEnabled;
        public static ConfigEntry<int> ParticleRaycastBudget;

        public static ConfigEntry<bool> SkipIntroEnabled;

        public static ConfigEntry<bool> FrameBudgetGuardEnabled;
        public static ConfigEntry<float> FrameBudgetThresholdMs;
        public static ConfigEntry<float> FrameBudgetThrottledDelta;
        public static ConfigEntry<float> FrameBudgetNormalDelta;


        private void Awake()
        {
            _harmony.PatchAll(typeof(ValheimPerformanceOverhaul.UI.WelcomeMessage));
            Log = Logger;
            Instance = this;

            SetupConfig();

            Log.LogInfo($"Initializing {PluginName} v{PluginVersion}...");

            if (GraphicsSettingsEnabled.Value)
                ApplyImmediateGraphicsSettings();

            Log.LogInfo("Applying Harmony patches...");
            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.LogInfo("All patches applied successfully.");
            }
            catch (System.Exception e)
            {
                Log.LogError($"Error applying patches: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Start()
        {
            if (ObjectPoolingEnabled.Value)
            {
                ObjectPoolManager.Initialize();
                Log.LogInfo("[ObjectPooling] System initialized.");
            }

            if (AudioPoolingEnabled.Value)
            {
                AudioPoolManager.Initialize();
                Log.LogInfo("[AudioPooling] System initialized.");
            }

            if (DistanceCullerEnabled.Value)
            {
                var manager = new GameObject("_VPO_DistanceCullerManager");
                manager.AddComponent<DistanceCullerManager>();
                DontDestroyOnLoad(manager);
                Log.LogInfo("[DistanceCuller] Manager initialized.");
            }

            if (AiThrottlingEnabled.Value)
            {
                var aiManager = new GameObject("_VPO_AIOptimizer");
                aiManager.AddComponent<AIOptimizer>();
                DontDestroyOnLoad(aiManager);
                Log.LogInfo("[AI] Optimizer manager initialized.");
            }

            if (FrameBudgetGuardEnabled.Value)
            {
                var guardObj = new GameObject("_VPO_FrameBudgetGuard");
                guardObj.AddComponent<FrameBudgetGuard>();
                DontDestroyOnLoad(guardObj);
                Log.LogInfo("[FrameBudgetGuard] Initialized.");
            }

            var memoryManager = new GameObject("_VPO_MemoryManager");
            memoryManager.AddComponent<MemoryManager>();
            DontDestroyOnLoad(memoryManager);
            Log.LogInfo("[MemoryManager] Initialized.");
        }

        private void SetupConfig()
        {
            DebugLoggingEnabled = Config.Bind(
                "1. General",
                "Enable Debug Logging",
                false,
                "Enables detailed diagnostic logs for troubleshooting.");

            GcControlEnabled = Config.Bind(
                "2. GC Control",
                "Enabled",
                true,
                "Prevents garbage collection during combat or movement to reduce stuttering.");

            DistanceCullerEnabled = Config.Bind(
                "3. Distance Culler",
                "Enabled",
                true,
                "Disables or throttles logic for distant objects to improve performance.");

            CreatureCullDistance = Config.Bind(
                "3. Distance Culler",
                "Creature Cull Distance",
                80f,
                new ConfigDescription(
                    "Distance at which creatures are put to sleep.",
                    new AcceptableValueRange<float>(40f, 200f)));

            PieceCullDistance = Config.Bind(
                "3. Distance Culler",
                "Piece Cull Distance",
                100f,
                new ConfigDescription(
                    "Distance at which build pieces are put to sleep.",
                    new AcceptableValueRange<float>(50f, 300f)));

            CullPhysicsEnabled = Config.Bind(
                "3. Distance Culler",
                "Enable Physics Culling",
                true,
                "Disables Rigidbody physics on distant objects.");

            AiThrottlingEnabled = Config.Bind(
                "3. Distance Culler",
                "Enable AI Throttling",
                true,
                "Reduces how often distant AI updates to save CPU cycles.");

            CullerExclusions = Config.Bind(
                "3. Distance Culler",
                "Exclusions",
                "TombStone,portal_wood",
                "Comma-separated list of prefab names to NEVER cull.");

            ObjectPoolingEnabled = Config.Bind(
                "4. Object Pooling",
                "Enabled",
                true,
                "Reuses ItemDrop objects to reduce instantiation overhead.");

            JitWarmupEnabled = Config.Bind(
                "5. JIT Warm-up",
                "Enabled",
                true,
                "Pre-compiles critical methods at game start to prevent initial stuttering.");

            LightCullingEnabled = Config.Bind(
                "6. Light Culling",
                "Enabled",
                true,
                "Disables distant light sources to improve performance. MAJOR FPS improvement!");

            MaxActiveLights = Config.Bind(
                "6. Light Culling",
                "Max Active Lights",
                15,
                new ConfigDescription(
                    "Maximum number of active lights near the player.",
                    new AcceptableValueRange<int>(5, 50)));

            LightCullDistance = Config.Bind(
                "6. Light Culling",
                "Light Cull Distance",
                60f,
                new ConfigDescription(
                    "Maximum distance for active lights.",
                    new AcceptableValueRange<float>(20f, 150f)));

            MaxShadowCasters = Config.Bind(
                "6. Light Culling",
                "Max Shadow Casters",
                5,
                new ConfigDescription(
                    "Maximum number of lights that can cast shadows simultaneously.",
                    new AcceptableValueRange<int>(0, 15)));

            ShadowCullDistance = Config.Bind(
                "6. Light Culling",
                "Shadow Cull Distance",
                30f,
                new ConfigDescription(
                    "Distance at which shadows are disabled.",
                    new AcceptableValueRange<float>(10f, 80f)));

            LightLODEnabled = Config.Bind(
                "6. Light Culling",
                "Enable Light LOD System",
                true,
                "Enables Level of Detail system for lights. MASSIVE FPS improvement!");

            LightLODFullDistance = Config.Bind(
                "6. Light Culling",
                "LOD Full Light Distance",
                20f,
                new ConfigDescription(
                    "Distance for full quality light with shadows.",
                    new AcceptableValueRange<float>(10f, 50f)));

            LightLODNoShadowDistance = Config.Bind(
                "6. Light Culling",
                "LOD No Shadow Distance",
                40f,
                new ConfigDescription(
                    "Distance at which shadows are removed but light remains.",
                    new AcceptableValueRange<float>(20f, 80f)));

            LightLODEmissiveDistance = Config.Bind(
                "6. Light Culling",
                "LOD Emissive Distance",
                70f,
                new ConfigDescription(
                    "Distance at which light is replaced with emissive material.",
                    new AcceptableValueRange<float>(40f, 120f)));

            LightLODBillboardDistance = Config.Bind(
                "6. Light Culling",
                "LOD Billboard Distance",
                100f,
                new ConfigDescription(
                    "Distance at which emissive is replaced with simple billboard.",
                    new AcceptableValueRange<float>(60f, 200f)));

            AudioPoolingEnabled = Config.Bind(
                "7. Audio Optimization",
                "Enabled",
                true,
                "Reuses sound effect objects to reduce allocations.");

            AudioPoolSize = Config.Bind(
                "7. Audio Optimization",
                "Total Pool Size",
                32,
                new ConfigDescription(
                    "Total number of reusable AudioSource components.",
                    new AcceptableValueRange<int>(16, 128)));

            GraphicsSettingsEnabled = Config.Bind(
                "8. Graphics Settings",
                "Enabled",
                true,
                "Enables advanced graphics settings module.");

            ConfigShadowDistance = Config.Bind(
                "8. Graphics Settings",
                "Shadow Distance",
                50f,
                new ConfigDescription(
                    "Maximum distance for shadow rendering.",
                    new AcceptableValueRange<float>(20f, 150f)));

            ConfigShadowResolution = Config.Bind(
                "8. Graphics Settings",
                "Shadow Resolution",
                512,
                new ConfigDescription(
                    "Shadow resolution quality.",
                    new AcceptableValueList<int>(512, 1024, 2048, 4096)));

            ConfigShadowCascades = Config.Bind(
                "8. Graphics Settings",
                "Shadow Cascades",
                1,
                new ConfigDescription(
                    "Number of shadow cascades.",
                    new AcceptableValueRange<int>(0, 4)));

            ConfigTerrainQuality = Config.Bind(
                "8. Graphics Settings",
                "Terrain Quality Multiplier",
                0.7f,
                new ConfigDescription(
                    "Terrain detail quality.",
                    new AcceptableValueRange<float>(0.1f, 2.0f)));

            ConfigReflections = Config.Bind(
                "8. Graphics Settings",
                "Enable Reflections",
                false,
                "Enables screen-space reflections.");

            ConfigBloom = Config.Bind(
                "8. Graphics Settings",
                "Enable Bloom",
                false,
                "Enables bloom glow effect.");

            PieceOptimizationEnabled = Config.Bind(
                "10. Piece Optimization",
                "Enabled",
                true,
                "Optimizes building piece updates and collision detection.");

            PieceUpdateInterval = Config.Bind(
                "10. Piece Optimization",
                "Update Interval (seconds)",
                2.0f,
                new ConfigDescription(
                    "How often building pieces update their state.",
                    new AcceptableValueRange<float>(0.5f, 10.0f)));

            PieceColliderDistance = Config.Bind(
                "10. Piece Optimization",
                "Collider Disable Distance",
                80f,
                new ConfigDescription(
                    "Distance at which building colliders are disabled.",
                    new AcceptableValueRange<float>(40f, 200f)));

            PieceSupportCacheDuration = Config.Bind(
                "10. Piece Optimization",
                "Support Cache Duration (seconds)",
                5.0f,
                new ConfigDescription(
                    "How long to cache building support calculations.",
                    new AcceptableValueRange<float>(1.0f, 30.0f)));

            PieceUpdateSkipDistance = Config.Bind(
                "10. Piece Optimization",
                "Update Skip Distance",
                50f,
                new ConfigDescription(
                    "Distance at which building pieces stop updating entirely.",
                    new AcceptableValueRange<float>(30f, 150f)));

            ParticleOptimizationEnabled = Config.Bind(
                "11. Particle Optimization",
                "Enabled",
                true,
                "Optimizes particle systems (fire, smoke, sparks).");

            ParticleCullDistance = Config.Bind(
                "11. Particle Optimization",
                "Particle Cull Distance",
                50f,
                new ConfigDescription(
                    "Distance at which particle systems are disabled.",
                    new AcceptableValueRange<float>(20f, 100f)));

            MaxActiveParticles = Config.Bind(
                "11. Particle Optimization",
                "Max Active Particle Systems",
                30,
                new ConfigDescription(
                    "Maximum number of active particle systems.",
                    new AcceptableValueRange<int>(10, 100)));

            VegetationOptimizationEnabled = Config.Bind(
                "12. Vegetation Optimization",
                "Enabled",
                true,
                "Optimizes grass, bushes and terrain details.");

            GrassRenderDistance = Config.Bind(
                "12. Vegetation Optimization",
                "Grass Render Distance",
                60f,
                new ConfigDescription(
                    "Distance at which grass is rendered.",
                    new AcceptableValueRange<float>(30f, 120f)));

            GrassDensityMultiplier = Config.Bind(
                "12. Vegetation Optimization",
                "Grass Density Multiplier",
                0.7f,
                new ConfigDescription(
                    "Grass density. 1.0 = vanilla, 0.5 = half grass.",
                    new AcceptableValueRange<float>(0.3f, 1.0f)));

            DetailObjectDistance = Config.Bind(
                "12. Vegetation Optimization",
                "Detail Object Distance",
                80f,
                new ConfigDescription(
                    "Distance for small objects (stones, sticks).",
                    new AcceptableValueRange<float>(40f, 150f)));

            DetailDensity = Config.Bind(
                "12. Vegetation Optimization",
                "Detail Density",
                0.7f,
                new ConfigDescription(
                    "Density of detail objects.",
                    new AcceptableValueRange<float>(0.3f, 1.0f)));

            TerrainMaxLOD = Config.Bind(
                "12. Vegetation Optimization",
                "Terrain Max LOD",
                1,
                new ConfigDescription(
                    "Maximum LOD level for terrain.",
                    new AcceptableValueRange<int>(0, 2)));

            AnimatorOptimizationEnabled = Config.Bind(
                "13. Animator Optimization",
                "Enabled",
                true,
                "Optimizes character animations.");

            MinimapOptimizationEnabled = Config.Bind(
                "15. Minimap Optimization",
                "Enabled",
                true,
                "Reduces minimap texture size and update frequency.");

            MinimapTextureSize = Config.Bind(
                "15. Minimap Optimization",
                "Minimap Texture Size",
                1024,
                new ConfigDescription(
                    "Minimap texture resolution.",
                    new AcceptableValueList<int>(512, 1024, 2048)));

            MinimapUpdateInterval = Config.Bind(
                "15. Minimap Optimization",
                "Update Interval (frames)",
                2,
                new ConfigDescription(
                    "Update minimap every N frames.",
                    new AcceptableValueRange<int>(1, 10)));

            TamedIdleOptimizationEnabled = Config.Bind(
                "16. Tamed Mob Idle Optimization",
                "Enabled",
                true,
                "Enables BASE IDLE MODE for tamed mobs on base. Dramatically reduces CPU load from idle tamed creatures.");

            TamedIdleDistanceFromCombat = Config.Bind(
                "16. Tamed Mob Idle Optimization",
                "Idle Distance From Combat (seconds)",
                5f,
                new ConfigDescription(
                    "Time after combat before mob can enter idle mode.",
                    new AcceptableValueRange<float>(3f, 30f)));

            TamedIdleBaseDetectionRadius = Config.Bind(
                "16. Tamed Mob Idle Optimization",
                "Base Detection Radius",
                30f,
                new ConfigDescription(
                    "Radius to detect player structures (base detection).",
                    new AcceptableValueRange<float>(15f, 60f)));

            TamedIdleCheckInterval = Config.Bind(
                "16. Tamed Mob Idle Optimization",
                "Check Interval (seconds)",
                1f,
                new ConfigDescription(
                    "How often to check if mob should enter/exit idle mode.",
                    new AcceptableValueRange<float>(0.5f, 5f)));


            LightFlickerOptimizationEnabled = Config.Bind(
                "17. Light Flicker Optimization",
                "Enabled",
                true,
                "Fixes light intensity to base value, eliminating Shadow Map recalculation every frame from fires and torches. " +
                "RECOMMENDED: Large performance gain on bases with many light sources.");

            SmokeOptimizationEnabled = Config.Bind(
                "18. Smoke Physics Optimization",
                "Enabled",
                true,
                "Replaces complex smoke aerodynamics with simple linear interpolation. " +
                "Dramatic CPU savings on large bases with many campfires.");

            SmokeLiftForce = Config.Bind(
                "18. Smoke Physics Optimization",
                "Smoke Lift Force",
                3.5f,
                new ConfigDescription(
                    "Upward force applied to smoke particles with simplified physics. Higher = faster rising smoke.",
                    new AcceptableValueRange<float>(1f, 10f)));

            EngineQualitySettingsEnabled = Config.Bind(
                "19. Engine Quality Settings",
                "Enabled",
                true,
                "Applies low-level Unity QualitySettings tweaks: disables soft particles and soft vegetation, " +
                "reduces particle raycast budget. Minor visual change, significant GPU gain.");

            ParticleRaycastBudget = Config.Bind(
                "19. Engine Quality Settings",
                "Particle Raycast Budget",
                1024,
                new ConfigDescription(
                    "Max number of particle ray-casts per frame. Vanilla default is 4096. Lower = less CPU.",
                    new AcceptableValueRange<int>(64, 4096)));

            SkipIntroEnabled = Config.Bind(
                "20. Skip Intro",
                "Enabled",
                true,
                "Skips Iron Gate and Coffee Stain logo screens on game startup. Saves 5–10 seconds per launch.");

            FrameBudgetGuardEnabled = Config.Bind(
                "21. Frame Budget Guard",
                "Enabled",
                true,
                "Dynamically limits Time.maximumDeltaTime when heavy frame spikes are detected, " +
                "preventing the Unity physics 'Death Spiral'. Converts hard freezes into brief slow-motion.");

            FrameBudgetThresholdMs = Config.Bind(
                "21. Frame Budget Guard",
                "Freeze Threshold (ms)",
                28f,
                new ConfigDescription(
                    "1% Low frametime threshold in milliseconds. If worst frames exceed this, throttling activates. " +
                    "28ms ≈ below 36 FPS on worst frames.",
                    new AcceptableValueRange<float>(16f, 100f)));

            FrameBudgetThrottledDelta = Config.Bind(
                "21. Frame Budget Guard",
                "Throttled MaxDeltaTime",
                0.045f,
                new ConfigDescription(
                    "Time.maximumDeltaTime when throttling is active (freeze detected). " +
                    "Lower = physics accuracy is sacrificed more to preserve smoothness.",
                    new AcceptableValueRange<float>(0.02f, 0.1f)));

            FrameBudgetNormalDelta = Config.Bind(
                "21. Frame Budget Guard",
                "Normal MaxDeltaTime",
                0.07f,
                new ConfigDescription(
                    "Time.maximumDeltaTime during normal gameplay. Unity default is 0.3333.",
                    new AcceptableValueRange<float>(0.03f, 0.2f)));
        }

        private void ApplyImmediateGraphicsSettings()
        {
            try
            {
                QualitySettings.shadowDistance = ConfigShadowDistance.Value;
                switch (ConfigShadowResolution.Value)
                {
                    case 512: QualitySettings.shadowResolution = ShadowResolution.Low; break;
                    case 1024: QualitySettings.shadowResolution = ShadowResolution.Medium; break;
                    case 2048: QualitySettings.shadowResolution = ShadowResolution.High; break;
                    case 4096: QualitySettings.shadowResolution = ShadowResolution.VeryHigh; break;
                    default: QualitySettings.shadowResolution = ShadowResolution.Low; break;
                }
                QualitySettings.shadowCascades = ConfigShadowCascades.Value;
                Log.LogInfo("[Graphics] Applied immediate graphics settings.");
            }
            catch (System.Exception e)
            {
                Log.LogError($"[Graphics] Failed to apply settings: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            Log.LogInfo($"Unpatching all {PluginName} methods.");
            _harmony?.UnpatchSelf();
        }
    }

    public class MemoryManager : MonoBehaviour
    {
        public static MemoryManager Instance { get; private set; }

        private float _cleanupTimer = 0f;
        private const float CLEANUP_INTERVAL = 60f;
        private float _forceGCTimer = 0f;
        private const float FORCE_GC_INTERVAL = 300f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Plugin.Log.LogInfo("[MemoryManager] Initialized");
        }

        private void Update()
        {
            _cleanupTimer += Time.deltaTime;
            _forceGCTimer += Time.deltaTime;

            if (_cleanupTimer >= CLEANUP_INTERVAL)
            {
                _cleanupTimer = 0f;
                PerformLightCleanup();
            }

            if (_forceGCTimer >= FORCE_GC_INTERVAL)
            {
                _forceGCTimer = 0f;
                if (ShouldPerformHeavyCleanup())
                    PerformHeavyCleanup();
            }
        }

        private bool ShouldPerformHeavyCleanup()
        {
            if (Player.m_localPlayer == null) return true;
            var player = Player.m_localPlayer;
            return player.IsAttached() || player.IsSleeping() || player.InPlaceMode();
        }

        private void PerformLightCleanup()
        {
            int beforeMB = (int)(System.GC.GetTotalMemory(false) / 1048576);
            Resources.UnloadUnusedAssets();
            int afterMB = (int)(System.GC.GetTotalMemory(false) / 1048576);
            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo($"[MemoryManager] Light cleanup: {beforeMB}MB -> {afterMB}MB");
        }

        private void PerformHeavyCleanup()
        {
            int beforeMB = (int)(System.GC.GetTotalMemory(false) / 1048576);
            Resources.UnloadUnusedAssets();
            System.GC.Collect(System.GC.MaxGeneration, System.GCCollectionMode.Forced);
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            int afterMB = (int)(System.GC.GetTotalMemory(false) / 1048576);
            Plugin.Log.LogInfo($"[MemoryManager] Heavy cleanup: {beforeMB}MB -> {afterMB}MB");
        }

        private void OnDestroy() { Instance = null; }
    }
}
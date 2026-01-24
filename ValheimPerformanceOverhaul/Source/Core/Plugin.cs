using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using ValheimPerformanceOverhaul.ObjectPooling;
using ValheimPerformanceOverhaul.LightCulling;
using ValheimPerformanceOverhaul.Audio;
using ValheimPerformanceOverhaul.AI;

namespace ValheimPerformanceOverhaul
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGUID = "com.Skarif.ValheimPerformanceOverhaul";
        private const string PluginName = "Valheim Performance Overhaul";
        private const string PluginVersion = "2.5.1";

        private readonly Harmony _harmony = new Harmony(PluginGUID);
        public static ManualLogSource Log;
        public static Plugin Instance;

        // Секция 1: Общие
        public static ConfigEntry<bool> DebugLoggingEnabled;

        // Секция 2: GC Control
        public static ConfigEntry<bool> GcControlEnabled;

        // Секция 3: Distance Culler
        public static ConfigEntry<bool> DistanceCullerEnabled;
        public static ConfigEntry<float> CreatureCullDistance;
        public static ConfigEntry<float> PieceCullDistance;
        public static ConfigEntry<bool> CullPhysicsEnabled;
        public static ConfigEntry<bool> AiThrottlingEnabled;
        public static ConfigEntry<string> CullerExclusions;

        // Секция 4: Object Pooling
        public static ConfigEntry<bool> ObjectPoolingEnabled;

        // Секция 5: JIT Warm-up
        public static ConfigEntry<bool> JitWarmupEnabled;

        // Секция 6: Light Culling
        public static ConfigEntry<bool> LightCullingEnabled;
        public static ConfigEntry<int> MaxActiveLights;
        public static ConfigEntry<float> LightCullDistance;
        public static ConfigEntry<int> MaxShadowCasters;
        public static ConfigEntry<float> ShadowCullDistance;

        // Light LOD System
        public static ConfigEntry<bool> LightLODEnabled;
        public static ConfigEntry<float> LightLODFullDistance;
        public static ConfigEntry<float> LightLODNoShadowDistance;
        public static ConfigEntry<float> LightLODEmissiveDistance;
        public static ConfigEntry<float> LightLODBillboardDistance;

        // Секция 7: Audio Optimization
        public static ConfigEntry<bool> AudioPoolingEnabled;
        public static ConfigEntry<int> AudioPoolSize;

        // Секция 8: Graphics Settings
        public static ConfigEntry<bool> GraphicsSettingsEnabled;
        public static ConfigEntry<float> ConfigShadowDistance;
        public static ConfigEntry<int> ConfigShadowResolution;
        public static ConfigEntry<int> ConfigShadowCascades;
        public static ConfigEntry<float> ConfigTerrainQuality;
        public static ConfigEntry<bool> ConfigReflections;
        public static ConfigEntry<bool> ConfigBloom;

        // Секция 9: Network Throttling
        public static ConfigEntry<bool> NetworkThrottlingEnabled;
        public static ConfigEntry<bool> NetworkCompressionEnabled;
        public static ConfigEntry<float> NetworkUpdateRate;
        public static ConfigEntry<int> NetworkQueueSize;
        public static ConfigEntry<int> NetworkSendRateMin;
        public static ConfigEntry<int> NetworkSendRateMax;
        public static ConfigEntry<int> NetworkCompressionThreshold;

        // Секция 10: Piece Optimization
        public static ConfigEntry<bool> PieceOptimizationEnabled;
        public static ConfigEntry<float> PieceUpdateInterval;
        public static ConfigEntry<float> PieceColliderDistance;
        public static ConfigEntry<float> PieceSupportCacheDuration;
        public static ConfigEntry<float> PieceUpdateSkipDistance;

        // Секция 11: Particle Optimization
        public static ConfigEntry<bool> ParticleOptimizationEnabled;
        public static ConfigEntry<float> ParticleCullDistance;
        public static ConfigEntry<int> MaxActiveParticles;

        // Секция 12: Vegetation Optimization
        public static ConfigEntry<bool> VegetationOptimizationEnabled;
        public static ConfigEntry<float> GrassRenderDistance;
        public static ConfigEntry<float> GrassDensityMultiplier;
        public static ConfigEntry<float> DetailObjectDistance;
        public static ConfigEntry<float> DetailDensity;
        public static ConfigEntry<int> TerrainMaxLOD;

        // Секция 13: Animator Optimization
        public static ConfigEntry<bool> AnimatorOptimizationEnabled;

        // Секция 14: ZDO Optimization
        public static ConfigEntry<bool> ZDOOptimizationEnabled;
        public static ConfigEntry<float> ZDOSyncInterval;

        // Секция 15: Minimap Optimization
        public static ConfigEntry<bool> MinimapOptimizationEnabled;
        public static ConfigEntry<int> MinimapTextureSize;
        public static ConfigEntry<int> MinimapUpdateInterval;

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            SetupConfig();

            Log.LogInfo($"Initializing {PluginName} v{PluginVersion}...");

            // Отложенная инициализация тяжелых систем
            if (GraphicsSettingsEnabled.Value)
            {
                ApplyImmediateGraphicsSettings();
            }

            Log.LogInfo($"Applying Harmony patches...");

            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.LogInfo($"All patches applied successfully.");
            }
            catch (System.Exception e)
            {
                Log.LogError($"Error applying patches: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Start()
        {
            // Отложенная инициализация менеджеров (избегаем lag spike при загрузке)
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
        }

        private void SetupConfig()
        {
            // Секция 1: Общие
            DebugLoggingEnabled = Config.Bind(
                "1. General",
                "Enable Debug Logging",
                false,
                "Enables detailed diagnostic logs for troubleshooting.");

            // Секция 2: GC Control
            GcControlEnabled = Config.Bind(
                "2. GC Control",
                "Enabled",
                true,
                "Prevents garbage collection during combat or movement to reduce stuttering.");

            // Секция 3: Distance Culler
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

            // Секция 4: Object Pooling
            ObjectPoolingEnabled = Config.Bind(
                "4. Object Pooling",
                "Enabled",
                true,
                "Reuses ItemDrop objects to reduce instantiation overhead.");

            // Секция 5: JIT Warm-up
            JitWarmupEnabled = Config.Bind(
                "5. JIT Warm-up",
                "Enabled",
                true,
                "Pre-compiles critical methods at game start to prevent initial stuttering.");

            // Секция 6: Light Culling
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

            // Light LOD System
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

            // Секция 7: Audio Optimization
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

            // Секция 8: Graphics Settings
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

            // Секция 9: Network Throttling
            NetworkThrottlingEnabled = Config.Bind(
                "9. Network Throttling",
                "Enabled",
                true,
                "Enables network optimization module.");

            NetworkCompressionEnabled = Config.Bind(
                "9. Network Throttling",
                "Enable Compression",
                true,
                "Enables Zstd compression for network traffic.");

            NetworkCompressionThreshold = Config.Bind(
                "9. Network Throttling",
                "Compression Threshold (bytes)",
                128,
                new ConfigDescription(
                    "Minimum packet size to compress. Smaller packets are not compressed.",
                    new AcceptableValueRange<int>(64, 512)));

            NetworkUpdateRate = Config.Bind(
                "9. Network Throttling",
                "Update Rate Multiplier",
                0.75f,
                new ConfigDescription(
                    "Reduces the frequency of network updates.",
                    new AcceptableValueRange<float>(0.1f, 1.0f)));

            NetworkQueueSize = Config.Bind(
                "9. Network Throttling",
                "Network Queue Size (Bytes)",
                10240,
                new ConfigDescription(
                    "Size of the outgoing message queue.",
                    new AcceptableValueRange<int>(2048, 65536)));

            NetworkSendRateMin = Config.Bind(
                "9. Network Throttling",
                "Min Send Rate (Bytes/s)",
                153600,
                new ConfigDescription(
                    "Minimum data send rate.",
                    new AcceptableValueRange<int>(65536, 1048576)));

            NetworkSendRateMax = Config.Bind(
                "9. Network Throttling",
                "Max Send Rate (Bytes/s)",
                204800,
                new ConfigDescription(
                    "Maximum data send rate.",
                    new AcceptableValueRange<int>(131072, 2097152)));

            // Секция 10: Piece Optimization
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

            // Секция 11: Particle Optimization
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

            // Секция 12: Vegetation Optimization
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

            // Секция 13: Animator Optimization
            AnimatorOptimizationEnabled = Config.Bind(
                "13. Animator Optimization",
                "Enabled",
                true,
                "Optimizes character animations.");

            // Секция 14: ZDO Optimization
            ZDOOptimizationEnabled = Config.Bind(
                "14. ZDO Optimization",
                "Enabled",
                true,
                "Reduces network synchronization overhead.");

            ZDOSyncInterval = Config.Bind(
                "14. ZDO Optimization",
                "Static Object Sync Interval",
                2.0f,
                new ConfigDescription(
                    "How often static objects sync over network.",
                    new AcceptableValueRange<float>(0.5f, 10.0f)));

            // Секция 15: Minimap Optimization
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
        }

        private void ApplyImmediateGraphicsSettings()
        {
            try
            {
                QualitySettings.shadowDistance = ConfigShadowDistance.Value;

                switch (ConfigShadowResolution.Value)
                {
                    case 512:
                        QualitySettings.shadowResolution = ShadowResolution.Low;
                        break;
                    case 1024:
                        QualitySettings.shadowResolution = ShadowResolution.Medium;
                        break;
                    case 2048:
                        QualitySettings.shadowResolution = ShadowResolution.High;
                        break;
                    case 4096:
                        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                        break;
                    default:
                        QualitySettings.shadowResolution = ShadowResolution.Low;
                        break;
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
}
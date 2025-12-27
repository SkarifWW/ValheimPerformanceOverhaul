using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ValheimPerformanceOverhaul.LightCulling
{
    public enum LightPriority
    {
        Critical = 0,    // Игрок, NPC, важные объекты
        High = 1,        // Костры, печи, активные источники
        Medium = 2,      // Факелы, свечи в базе
        Low = 3,         // Декоративное освещение
        VeryLow = 4      // Дальние факелы, фоновое освещение
    }

    public class TrackedLight : MonoBehaviour
    {
        public Light LightSource { get; private set; }
        public LightPriority Priority { get; private set; }

        private bool _isCurrentlyCulled = false;
        private bool _hadShadows = false;
        private float _originalIntensity;
        private float _originalRange;
        private LightShadows _originalShadows;

        private void Awake()
        {
            LightSource = GetComponent<Light>();
            if (LightSource != null)
            {
                _originalIntensity = LightSource.intensity;
                _originalRange = LightSource.range;
                _originalShadows = LightSource.shadows;
                _hadShadows = _originalShadows != LightShadows.None;

                DeterminePriority();
            }
        }

        private void DeterminePriority()
        {
            if (LightSource == null)
            {
                Priority = LightPriority.VeryLow;
                return;
            }

            // Проверяем родителя и тип объекта
            var parentName = transform.parent != null ? transform.parent.name.ToLower() : "";
            var objectName = gameObject.name.ToLower();

            // Критичные источники (игрок, важные NPC)
            if (transform.root.GetComponent<Player>() != null)
            {
                Priority = LightPriority.Critical;
                return;
            }

            // Высокий приоритет (активные источники)
            if (parentName.Contains("fire") || parentName.Contains("hearth") ||
                parentName.Contains("brazier") || objectName.Contains("fire"))
            {
                Priority = LightPriority.High;
                return;
            }

            // Средний приоритет (факелы, свечи)
            if (parentName.Contains("torch") || parentName.Contains("candle") ||
                objectName.Contains("torch") || objectName.Contains("candle"))
            {
                Priority = LightPriority.Medium;
                return;
            }

            // Яркие источники = выше приоритет
            if (LightSource.intensity > 2.0f)
            {
                Priority = LightPriority.High;
            }
            else if (LightSource.intensity > 1.0f)
            {
                Priority = LightPriority.Medium;
            }
            else
            {
                Priority = LightPriority.Low;
            }
        }

        public void SetCulled(bool cull, bool disableShadows = false)
        {
            if (LightSource == null) return;

            if (cull)
            {
                if (!_isCurrentlyCulled)
                {
                    _originalIntensity = LightSource.intensity;
                    _originalRange = LightSource.range;

                    LightSource.intensity = 0f;
                    LightSource.range = 0f;

                    // НОВОЕ: Опциональное отключение теней
                    if (disableShadows && _hadShadows)
                    {
                        _originalShadows = LightSource.shadows;
                        LightSource.shadows = LightShadows.None;
                    }
                }
            }
            else
            {
                if (_isCurrentlyCulled)
                {
                    LightSource.intensity = _originalIntensity;
                    LightSource.range = _originalRange;

                    // Восстанавливаем тени только если они были
                    if (_hadShadows)
                    {
                        LightSource.shadows = _originalShadows;
                    }
                }
            }

            _isCurrentlyCulled = cull;
        }

        public void SetShadowsOnly(bool enabled)
        {
            if (LightSource == null || !_hadShadows) return;

            LightSource.shadows = enabled ? _originalShadows : LightShadows.None;
        }

        public bool IsCulled => _isCurrentlyCulled;
        public bool IsActiveLight => LightSource != null && LightSource.enabled && LightSource.intensity > 0.01f;
        public bool HasShadows => _hadShadows;
    }

    public class AdvancedLightManager : MonoBehaviour
    {
        public static AdvancedLightManager Instance { get; private set; }

        private readonly List<TrackedLight> _allLights = new List<TrackedLight>(512);
        private readonly HashSet<TrackedLight> _culledLights = new HashSet<TrackedLight>();

        private struct LightInfo
        {
            public TrackedLight Light;
            public float DistanceSqr;
            public LightPriority Priority;
        }
        private LightInfo[] _lightInfos = new LightInfo[512];
        private int _lightInfoCount = 0;

        private float _updateTimer;
        private float _scanTimer;
        private const float UPDATE_INTERVAL = 0.25f;
        private const float SCAN_INTERVAL = 5f;

        // НОВЫЕ НАСТРОЙКИ
        private int _maxActiveLights = 15;
        private int _maxShadowCasters = 5; // Отдельный лимит для теней!
        private float _lightCullDistance = 60f;
        private float _shadowCullDistance = 30f; // Тени отключаем раньше

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadConfig();
        }

        private void LoadConfig()
        {
            _maxActiveLights = Plugin.MaxActiveLights?.Value ?? 15;
            _lightCullDistance = Plugin.LightCullDistance?.Value ?? 60f;

            // НОВЫЕ КОНФИГИ (добавьте в Plugin.cs)
            // _maxShadowCasters = Plugin.MaxShadowCasters?.Value ?? 5;
            // _shadowCullDistance = Plugin.ShadowCullDistance?.Value ?? 30f;
        }

        private void Start()
        {
            if (!Plugin.LightCullingEnabled.Value)
            {
                enabled = false;
                return;
            }

            Plugin.Log.LogInfo("[AdvancedLightCulling] Starting with shadow management...");
            PerformLightScan();
            Plugin.Log.LogInfo($"[AdvancedLightCulling] Found {_allLights.Count} lights.");
        }

        private void PerformLightScan()
        {
            Light[] existingLights = FindObjectsOfType<Light>(true);
            foreach (var light in existingLights)
            {
                TryRegisterLight(light);
            }
        }

        public void TryRegisterLight(Light light)
        {
            if (light == null || light.GetComponent<TrackedLight>() != null) return;

            if (light.type == LightType.Directional || light.intensity <= 0.1f)
            {
                return;
            }

            var tracked = light.gameObject.AddComponent<TrackedLight>();
            _allLights.Add(tracked);

            if (_allLights.Count > _lightInfos.Length)
            {
                System.Array.Resize(ref _lightInfos, _lightInfos.Length * 2);
            }

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo($"[LightCulling] Registered: {light.name} (Priority: {tracked.Priority})");
        }

        private void OnDestroy()
        {
            foreach (var light in _allLights)
            {
                if (light != null)
                {
                    light.SetCulled(false);
                }
            }
            Instance = null;
        }

        private void Update()
        {
            if (Camera.main == null || Player.m_localPlayer == null) return;

            _updateTimer += Time.deltaTime;
            _scanTimer += Time.deltaTime;

            if (_scanTimer >= SCAN_INTERVAL)
            {
                _scanTimer = 0f;
                PerformLightScan();
            }

            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            UpdateLightCulling();
        }

        private void UpdateLightCulling()
        {
            _allLights.RemoveAll(l => l == null || l.LightSource == null);
            if (_allLights.Count == 0) return;

            Vector3 cameraPos = Camera.main.transform.position;
            float lightCullDistSqr = _lightCullDistance * _lightCullDistance;
            float shadowCullDistSqr = _shadowCullDistance * _shadowCullDistance;

            _lightInfoCount = 0;

            // Собираем информацию о всех источниках
            for (int i = 0; i < _allLights.Count; i++)
            {
                var light = _allLights[i];
                if (light == null || light.LightSource == null) continue;

                float distSqr = (cameraPos - light.transform.position).sqrMagnitude;

                if (distSqr <= lightCullDistSqr)
                {
                    _lightInfos[_lightInfoCount] = new LightInfo
                    {
                        Light = light,
                        DistanceSqr = distSqr,
                        Priority = light.Priority
                    };
                    _lightInfoCount++;
                }
                else
                {
                    // Слишком далеко - полное отключение
                    if (!light.IsCulled)
                    {
                        light.SetCulled(true, true);
                        _culledLights.Add(light);
                    }
                }
            }

            // КРИТИЧНО: Сортируем по приоритету, ЗАТЕМ по расстоянию
            System.Array.Sort(_lightInfos, 0, _lightInfoCount,
                Comparer<LightInfo>.Create((a, b) =>
                {
                    int priorityCompare = a.Priority.CompareTo(b.Priority);
                    if (priorityCompare != 0) return priorityCompare;
                    return a.DistanceSqr.CompareTo(b.DistanceSqr);
                }));

            int activeLightCount = 0;
            int shadowCasterCount = 0;

            for (int i = 0; i < _lightInfoCount; i++)
            {
                var info = _lightInfos[i];
                var light = info.Light;

                bool withinShadowDistance = info.DistanceSqr <= shadowCullDistSqr;
                bool canHaveShadows = withinShadowDistance &&
                                     shadowCasterCount < _maxShadowCasters &&
                                     light.HasShadows;

                // Решаем, активен ли источник
                bool shouldBeActive = activeLightCount < _maxActiveLights;

                if (shouldBeActive)
                {
                    // Включаем источник
                    if (light.IsCulled)
                    {
                        light.SetCulled(false);
                        _culledLights.Remove(light);
                    }

                    // Управляем тенями отдельно
                    light.SetShadowsOnly(canHaveShadows);

                    if (canHaveShadows)
                        shadowCasterCount++;

                    activeLightCount++;
                }
                else
                {
                    // Отключаем источник
                    if (!light.IsCulled)
                    {
                        light.SetCulled(true, true);
                        _culledLights.Add(light);
                    }
                }
            }

            if (Plugin.DebugLoggingEnabled.Value && Time.frameCount % 60 == 0)
            {
                Plugin.Log.LogInfo($"[LightCulling] Active: {activeLightCount}/{_maxActiveLights}, Shadows: {shadowCasterCount}/{_maxShadowCasters}");
            }
        }

        public void ForceUpdate()
        {
            _updateTimer = UPDATE_INTERVAL;
        }

        public int TotalLights => _allLights.Count;
        public int CulledLights => _culledLights.Count;
        public int ActiveLights => _allLights.Count - _culledLights.Count;
    }
}
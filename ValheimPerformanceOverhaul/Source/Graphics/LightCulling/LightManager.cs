using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.LightCulling
{
    public enum LightPriority
    {
        Critical = 0,
        High = 1,
        Medium = 2,
        Low = 3,
        VeryLow = 4
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

            var parentName = transform.parent != null ? transform.parent.name.ToLower() : "";
            var objectName = gameObject.name.ToLower();

            if (transform.root.GetComponent<Player>() != null)
            {
                Priority = LightPriority.Critical;
                return;
            }

            if (parentName.Contains("fire") || parentName.Contains("hearth") ||
                parentName.Contains("brazier") || objectName.Contains("fire"))
            {
                Priority = LightPriority.High;
                return;
            }

            if (parentName.Contains("torch") || parentName.Contains("candle") ||
                objectName.Contains("torch") || objectName.Contains("candle"))
            {
                Priority = LightPriority.Medium;
                return;
            }

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
        private int _cleanupCounter = 0; // ✅ НОВОЕ

        private const float UPDATE_INTERVAL = 0.25f;
        private const float SCAN_INTERVAL = 5f;

        private int _maxActiveLights = 15;
        private int _maxShadowCasters = 5;
        private float _lightCullDistance = 60f;
        private float _shadowCullDistance = 30f;

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
            _maxShadowCasters = Plugin.MaxShadowCasters?.Value ?? 5;
            _shadowCullDistance = Plugin.ShadowCullDistance?.Value ?? 30f;
        }

        private void Start()
        {
            if (!Plugin.LightCullingEnabled.Value)
            {
                enabled = false;
                return;
            }

            Plugin.Log.LogInfo("[LightCulling] Advanced manager starting...");
            PerformLightScan();
            Plugin.Log.LogInfo($"[LightCulling] Found {_allLights.Count} lights.");
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
                return;

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

            // ✅ НОВОЕ: Ленивая очистка (каждые 10 обновлений)
            _cleanupCounter++;
            if (_cleanupCounter >= 10)
            {
                _cleanupCounter = 0;
                CleanupNullLights();
            }

            UpdateLightCulling();
        }

        // ✅ НОВОЕ: Оптимизированная очистка
        private void CleanupNullLights()
        {
            for (int i = _allLights.Count - 1; i >= 0; i--)
            {
                if (_allLights[i] == null || _allLights[i].LightSource == null)
                {
                    _allLights.RemoveAt(i);
                }
            }
        }

        private void UpdateLightCulling()
        {
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
                    if (!light.IsCulled)
                    {
                        light.SetCulled(true, true);
                        _culledLights.Add(light);
                    }
                }
            }

            // ✅ ИСПРАВЛЕНО: Используем partial sort (QuickSelect)
            // Вместо полной сортировки O(n log n) используем частичную O(n)
            QuickSelectTopN(_lightInfos, _lightInfoCount, _maxActiveLights);

            int activeLightCount = 0;
            int shadowCasterCount = 0;

            // Обрабатываем только топ-N источников
            int processCount = System.Math.Min(_lightInfoCount, _maxActiveLights * 2);

            for (int i = 0; i < processCount; i++)
            {
                var info = _lightInfos[i];
                var light = info.Light;

                bool withinShadowDistance = info.DistanceSqr <= shadowCullDistSqr;
                bool canHaveShadows = withinShadowDistance &&
                                     shadowCasterCount < _maxShadowCasters &&
                                     light.HasShadows;

                bool shouldBeActive = activeLightCount < _maxActiveLights;

                if (shouldBeActive)
                {
                    if (light.IsCulled)
                    {
                        light.SetCulled(false);
                        _culledLights.Remove(light);
                    }

                    light.SetShadowsOnly(canHaveShadows);

                    if (canHaveShadows)
                        shadowCasterCount++;

                    activeLightCount++;
                }
                else
                {
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

        // ✅ НОВОЕ: QuickSelect алгоритм для частичной сортировки O(n)
        private void QuickSelectTopN(LightInfo[] array, int length, int topN)
        {
            if (length <= topN) return;

            // Сортируем только первые topN элементов
            int left = 0;
            int right = length - 1;
            topN = System.Math.Min(topN, length);

            while (left < right)
            {
                int pivotIndex = Partition(array, left, right);

                if (pivotIndex == topN)
                    break;
                else if (pivotIndex < topN)
                    left = pivotIndex + 1;
                else
                    right = pivotIndex - 1;
            }

            // Сортируем только первые topN элементов
            System.Array.Sort(array, 0, System.Math.Min(topN, length),
                System.Collections.Generic.Comparer<LightInfo>.Create((a, b) =>
                {
                    int priorityCompare = a.Priority.CompareTo(b.Priority);
                    if (priorityCompare != 0) return priorityCompare;
                    return a.DistanceSqr.CompareTo(b.DistanceSqr);
                }));
        }

        private int Partition(LightInfo[] array, int left, int right)
        {
            var pivot = array[right];
            int i = left - 1;

            for (int j = left; j < right; j++)
            {
                int priorityCompare = array[j].Priority.CompareTo(pivot.Priority);
                bool shouldSwap = priorityCompare < 0 ||
                    (priorityCompare == 0 && array[j].DistanceSqr < pivot.DistanceSqr);

                if (shouldSwap)
                {
                    i++;
                    var temp = array[i];
                    array[i] = array[j];
                    array[j] = temp;
                }
            }

            var temp2 = array[i + 1];
            array[i + 1] = array[right];
            array[right] = temp2;

            return i + 1;
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
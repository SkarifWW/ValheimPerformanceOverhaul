using System.Collections.Generic;
using UnityEngine;

namespace ValheimPerformanceOverhaul.Core
{
    public class FrameBudgetGuard : MonoBehaviour
    {
        public static FrameBudgetGuard Instance { get; private set; }


        private const int SAMPLE_CAPACITY = 120; // ~2 секунды при 60 FPS
        private readonly Queue<float> _frametimeQueue = new Queue<float>(SAMPLE_CAPACITY);
        private readonly List<float> _sortBuffer = new List<float>(SAMPLE_CAPACITY);

        private float _current1PctLowMs = 0f;


        private float _checkTimer = 0f;
        private const float CHECK_INTERVAL = 0.5f; // проверяем каждые 0.5 секунды

        private bool _isThrottling = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Plugin.Log.LogInfo("[FrameBudgetGuard] Initialized.");
        }

        private void Update()
        {
            if (!Plugin.FrameBudgetGuardEnabled.Value) return;

            float frameMs = Time.unscaledDeltaTime * 1000f;

            if (_frametimeQueue.Count >= SAMPLE_CAPACITY)
                _frametimeQueue.Dequeue();

            _frametimeQueue.Enqueue(frameMs);

            _checkTimer += Time.unscaledDeltaTime;
            if (_checkTimer < CHECK_INTERVAL) return;
            _checkTimer = 0f;

            _current1PctLowMs = Calculate1PctLow();
            ApplyFrameBudget(_current1PctLowMs);
        }

        private float Calculate1PctLow()
        {
            if (_frametimeQueue.Count == 0) return 0f;

            _sortBuffer.Clear();
            foreach (float ms in _frametimeQueue)
                _sortBuffer.Add(ms);

            _sortBuffer.Sort((a, b) => b.CompareTo(a));

            int worstCount = Mathf.Max(1, _sortBuffer.Count / 100);
            float sum = 0f;
            for (int i = 0; i < worstCount; i++)
                sum += _sortBuffer[i];

            return sum / worstCount;
        }

        private void ApplyFrameBudget(float worstMs)
        {
            float threshold = Plugin.FrameBudgetThresholdMs.Value;
            float throttledMaxDelta = Plugin.FrameBudgetThrottledDelta.Value;
            float normalMaxDelta = Plugin.FrameBudgetNormalDelta.Value;

            if (worstMs > threshold && !_isThrottling)
            {
                Time.maximumDeltaTime = throttledMaxDelta;
                _isThrottling = true;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogWarning(
                        $"[FrameBudgetGuard] Throttling ON. 1% Low: {worstMs:F1}ms > {threshold}ms. " +
                        $"MaxDelta: {throttledMaxDelta:F3}s");
            }
            else if (worstMs <= threshold && _isThrottling)
            {
                Time.maximumDeltaTime = normalMaxDelta;
                _isThrottling = false;

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo(
                        $"[FrameBudgetGuard] Throttling OFF. 1% Low: {worstMs:F1}ms. " +
                        $"MaxDelta restored: {normalMaxDelta:F3}s");
            }
        }

        public float Current1PctLowMs => _current1PctLowMs;
        public bool IsThrottling => _isThrottling;

        private void OnDestroy()
        {
            Time.maximumDeltaTime = 0.3333f;
            Instance = null;
        }
    }
}
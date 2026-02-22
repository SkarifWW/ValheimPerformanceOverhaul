using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.Audio
{
    public class PooledAudio : MonoBehaviour
    {
        public AudioSource Source { get; private set; }
        private bool _isAvailable = true;
        private float _playStartTime;
        private float _clipLength;

        private void Awake()
        {
            Source = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (!_isAvailable && Source != null)
            {
                                float expectedEndTime = _playStartTime + _clipLength + 0.1f; 
                                                                                if (Time.time >= expectedEndTime && !Source.isPlaying)
                {
                    SetAvailable(true);
                }
            }
        }

        public void Play(AudioSource original)
        {
            if (Source == null || original == null || original.clip == null) return;

            _isAvailable = false;
            _playStartTime = Time.time;

                        _clipLength = original.clip.length;
            if (original.pitch > 0.01f)             {
                _clipLength /= Mathf.Abs(original.pitch);
            }

                        Source.clip = original.clip;
            Source.volume = original.volume;
            Source.pitch = original.pitch;
            Source.loop = original.loop;             Source.outputAudioMixerGroup = original.outputAudioMixerGroup;
            Source.spatialBlend = original.spatialBlend;
            Source.rolloffMode = original.rolloffMode;
            Source.minDistance = original.minDistance;
            Source.maxDistance = original.maxDistance;
            Source.spread = original.spread;
            Source.dopplerLevel = original.dopplerLevel;
            Source.priority = original.priority;

                        Source.transform.position = original.transform.position;

                        Source.Play();

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[AudioPooling] Playing clip: {original.clip.name}, length: {_clipLength:F2}s, pitch: {original.pitch:F2}");
            }
        }

        public void SetAvailable(bool available)
        {
            if (_isAvailable == available) return;
            _isAvailable = available;

            if (available)
            {
                                if (Source != null)
                {
                    Source.Stop();
                    Source.clip = null;
                }

                AudioPoolManager.ReturnToPool(this);
            }
        }

        public bool IsAvailable => _isAvailable;

        private void OnDestroy()
        {
            if (Source != null)
            {
                Source.Stop();
            }
        }
    }

    public static class AudioPoolManager
    {
        private static readonly Stack<PooledAudio> _availablePool = new Stack<PooledAudio>();
        private static readonly List<PooledAudio> _allSources = new List<PooledAudio>();
        private static GameObject _poolObject;
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            _poolObject = new GameObject("_VPO_AudioPool");
            Object.DontDestroyOnLoad(_poolObject);

            for (int i = 0; i < Plugin.AudioPoolSize.Value; i++)
            {
                CreatePooledAudioSource();
            }

            _isInitialized = true;
            Plugin.Log.LogInfo($"[AudioPooling] Initialized with {_availablePool.Count} sources.");
        }

        private static void CreatePooledAudioSource()
        {
            var audioSourceObj = new GameObject($"PooledAudio_{_allSources.Count}");
            audioSourceObj.transform.SetParent(_poolObject.transform);

            var audioSource = audioSourceObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 6f;
            audioSource.maxDistance = 40f;
            audioSource.spread = 120f;
            audioSource.dopplerLevel = 0;
            audioSource.priority = 128; 
            var pooledAudio = audioSourceObj.AddComponent<PooledAudio>();
            _allSources.Add(pooledAudio);
            _availablePool.Push(pooledAudio);
        }

        public static bool TryPlayClip(AudioSource originalSource)
        {
            if (!_isInitialized || originalSource == null || originalSource.clip == null)
            {
                return false;
            }

            
                        if (originalSource.outputAudioMixerGroup != null)
            {
                string groupName = originalSource.outputAudioMixerGroup.name.ToLower();
                if (groupName.Contains("music") || groupName.Contains("gui") || groupName.Contains("voice"))
                {
                    return false;
                }
            }

                        if (originalSource.loop)
            {
                return false;
            }

                        if (originalSource.spatialBlend < 0.1f)
            {
                return false;
            }

                        if (originalSource.clip.length > 10f)
            {
                return false;
            }

                        if (_availablePool.Count > 0)
            {
                var pooledAudio = _availablePool.Pop();
                pooledAudio.Play(originalSource);
                return true;
            }

                        if (_allSources.Count < Plugin.AudioPoolSize.Value * 2)             {
                CreatePooledAudioSource();
                if (_availablePool.Count > 0)
                {
                    var pooledAudio = _availablePool.Pop();
                    pooledAudio.Play(originalSource);
                    return true;
                }
            }

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogWarning($"[AudioPooling] Pool exhausted. Active: {_allSources.Count - _availablePool.Count}/{_allSources.Count}");
            }

            return false;
        }

        internal static void ReturnToPool(PooledAudio pooledAudio)
        {
            if (pooledAudio == null) return;

                        if (!_availablePool.Contains(pooledAudio))
            {
                _availablePool.Push(pooledAudio);

                if (Plugin.DebugLoggingEnabled.Value)
                {
                    Plugin.Log.LogInfo($"[AudioPooling] Returned to pool. Available: {_availablePool.Count}/{_allSources.Count}");
                }
            }
        }

        public static void Shutdown()
        {
            _isInitialized = false;
            _availablePool.Clear();
            _allSources.Clear();

            if (_poolObject != null)
            {
                Object.Destroy(_poolObject);
                _poolObject = null;
            }
        }
    }
}
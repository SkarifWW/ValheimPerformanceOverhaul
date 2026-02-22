using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.LightCulling
{
    public enum LightLODLevel
    {
        Full = 0,
        NoShadows = 1,
        Emissive = 2,
        Billboard = 3,
        Disabled = 4
    }

    public class LightLOD : MonoBehaviour
    {
        public Light LightSource { get; private set; }

        private MeshRenderer _meshRenderer;
        private Material _emissiveMaterial;
        private GameObject _billboardObject;
        private SpriteRenderer _billboardRenderer;

        private float _originalIntensity;
        private float _originalRange;
        private LightShadows _originalShadows;
        private Material _originalMaterial;
        private Color _lightColor;

        private LightLODLevel _currentLOD = LightLODLevel.Full;
        public LightPriority Priority { get; private set; }

        private static Texture2D _glowTexture;
        private static Material _billboardMaterial;
        private static Shader _billboardShader;

        private void Awake()
        {
            LightSource = GetComponent<Light>();
            if (LightSource == null) return;

            _originalIntensity = LightSource.intensity;
            _originalRange = LightSource.range;
            _originalShadows = LightSource.shadows;
            _lightColor = LightSource.color;

            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (_meshRenderer != null)
                _originalMaterial = _meshRenderer.material;

            DeterminePriority();
            InitializeResources();
        }

        private void DeterminePriority()
        {
            if (LightSource == null) { Priority = LightPriority.VeryLow; return; }

            var parentName = transform.parent != null ? transform.parent.name.ToLower() : "";
            var objectName = gameObject.name.ToLower();

            if (transform.root.GetComponent<Player>() != null)
            {
                Priority = LightPriority.Critical;
            }
            else if (parentName.Contains("fire") || parentName.Contains("hearth"))
            {
                Priority = LightPriority.High;
            }
            else if (parentName.Contains("torch") || objectName.Contains("torch"))
            {
                Priority = LightPriority.Medium;
            }
            else if (LightSource.intensity > 2.0f)
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

        private void InitializeResources()
        {
            if (_glowTexture == null)
                _glowTexture = CreateGlowTexture();

            if (_billboardShader == null)
            {
                _billboardShader = Shader.Find("Sprites/Default");
                if (_billboardShader == null)
                    _billboardShader = Shader.Find("Unlit/Transparent");
            }

            if (_billboardMaterial == null && _billboardShader != null)
            {
                _billboardMaterial = new Material(_billboardShader);
                _billboardMaterial.mainTexture = _glowTexture;
                _billboardMaterial.SetInt("_ZWrite", 0);
                _billboardMaterial.renderQueue = 3000;
            }
        }

        private static Texture2D CreateGlowTexture()
        {
            int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - (dist / maxDist)), 2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private void CreateBillboard()
        {
            if (_billboardObject != null) return;

            _billboardObject = new GameObject("LightBillboard");
            _billboardObject.transform.SetParent(transform);
            _billboardObject.transform.localPosition = Vector3.zero;

            _billboardRenderer = _billboardObject.AddComponent<SpriteRenderer>();
            var sprite = Sprite.Create(
                _glowTexture,
                new Rect(0, 0, _glowTexture.width, _glowTexture.height),
                new Vector2(0.5f, 0.5f));

            _billboardRenderer.sprite = sprite;
            _billboardRenderer.material = _billboardMaterial;
            _billboardRenderer.color = _lightColor;

            float scale = Mathf.Lerp(0.5f, 2.0f, _originalIntensity / 3.0f);
            _billboardObject.transform.localScale = Vector3.one * scale;
            _billboardObject.SetActive(false);
        }

        private void CreateEmissiveMaterial()
        {
            if (_meshRenderer == null || _emissiveMaterial != null) return;

            _emissiveMaterial = new Material(_originalMaterial);
            _emissiveMaterial.EnableKeyword("_EMISSION");
            _emissiveMaterial.SetColor("_EmissionColor", _lightColor * _originalIntensity * 0.5f);

            if (!_emissiveMaterial.HasProperty("_EmissionColor"))
                _emissiveMaterial.SetColor("_Color", _lightColor * 1.5f);
        }

        public void SetLODLevel(LightLODLevel level)
        {
            if (_currentLOD == level || LightSource == null) return;

            DisableCurrentLOD();
            _currentLOD = level;

            switch (level)
            {
                case LightLODLevel.Full: EnableFullLight(); break;
                case LightLODLevel.NoShadows: EnableLightNoShadows(); break;
                case LightLODLevel.Emissive: EnableEmissive(); break;
                case LightLODLevel.Billboard: EnableBillboard(); break;
                case LightLODLevel.Disabled: break;
            }

            if (Plugin.DebugLoggingEnabled.Value)
                Plugin.Log.LogInfo($"[LightLOD] {gameObject.name} switched to LOD {level}");
        }

        private void DisableCurrentLOD()
        {
            if (LightSource != null) LightSource.enabled = false;
            if (_meshRenderer != null && _originalMaterial != null)
                _meshRenderer.material = _originalMaterial;
            if (_billboardObject != null)
                _billboardObject.SetActive(false);
        }

        private void EnableFullLight()
        {
            LightSource.enabled = true;
            LightSource.intensity = _originalIntensity;
            LightSource.range = _originalRange;
            LightSource.shadows = _originalShadows;
        }

        private void EnableLightNoShadows()
        {
            LightSource.enabled = true;
            LightSource.intensity = _originalIntensity;
            LightSource.range = _originalRange;
            LightSource.shadows = LightShadows.None;
        }

        private void EnableEmissive()
        {
            if (_meshRenderer == null) return;
            if (_emissiveMaterial == null) CreateEmissiveMaterial();
            if (_emissiveMaterial != null)
                _meshRenderer.material = _emissiveMaterial;
        }

        private void EnableBillboard()
        {
            if (_billboardObject == null) CreateBillboard();
            if (_billboardObject != null)
                _billboardObject.SetActive(true);
        }

        public LightLODLevel CurrentLOD => _currentLOD;

        private void OnDestroy()
        {
            if (_emissiveMaterial != null) Destroy(_emissiveMaterial);
            if (_billboardObject != null) Destroy(_billboardObject);
        }
    }

    // =========================================================================
    // LightLODManager — FIX: ScanForLights() removed from Start().
    //
    // Причина: LightLODPatches.RegisterNewLights() реактивно вызывает
    // RegisterLight() при каждом ZNetScene.CreateObject, поэтому
    // повторное сканирование сцены в Start() создавало дубликаты в _allLights.
    // При старте игры сцена ещё пустая, так что скан был и бесполезен.
    // =========================================================================
    public class LightLODManager : MonoBehaviour
    {
        public static LightLODManager Instance { get; private set; }

        private readonly List<LightLOD> _allLights = new List<LightLOD>(512);
        private float _updateTimer;
        private int _cleanupCounter;

        private const float UPDATE_INTERVAL = 0.5f;

        private float _fullLODDistance = 20f;
        private float _noShadowDistance = 40f;
        private float _emissiveDistance = 70f;
        private float _billboardDistance = 100f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            LoadConfig();
            // FIX: ScanForLights() call removed.
            // Lights are registered reactively via LightLODPatches.RegisterNewLights()
            // which hooks ZNetScene.CreateObject — no duplicate entries, no FPS spike on load.
            Plugin.Log.LogInfo("[LightLOD] Manager initialized. Reactive registration active.");
        }

        private void LoadConfig()
        {
            _fullLODDistance = Plugin.LightLODFullDistance?.Value ?? 20f;
            _noShadowDistance = Plugin.LightLODNoShadowDistance?.Value ?? 40f;
            _emissiveDistance = Plugin.LightLODEmissiveDistance?.Value ?? 70f;
            _billboardDistance = Plugin.LightLODBillboardDistance?.Value ?? 100f;
        }

        /// <summary>
        /// Called by LightLODPatches on every ZNetScene.CreateObject.
        /// Adds a LightLOD component if not already present and tracks it.
        /// </summary>
        public void RegisterLight(Light light)
        {
            if (light == null || light.type == LightType.Directional) return;

            var lodComponent = light.GetComponent<LightLOD>();
            if (lodComponent == null)
                lodComponent = light.gameObject.AddComponent<LightLOD>();

            if (!_allLights.Contains(lodComponent))
                _allLights.Add(lodComponent);
        }

        private void Update()
        {
            if (Player.m_localPlayer == null || Camera.main == null) return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            _cleanupCounter++;
            if (_cleanupCounter >= 10)
            {
                _cleanupCounter = 0;
                CleanupNullLights();
            }

            UpdateLODs();
        }

        private void CleanupNullLights()
        {
            for (int i = _allLights.Count - 1; i >= 0; i--)
            {
                if (_allLights[i] == null || _allLights[i].LightSource == null)
                    _allLights.RemoveAt(i);
            }
        }

        private void UpdateLODs()
        {
            Vector3 cameraPos = Camera.main.transform.position;

            foreach (var lightLOD in _allLights)
            {
                if (lightLOD == null || lightLOD.LightSource == null) continue;

                float distance = Vector3.Distance(cameraPos, lightLOD.transform.position);

                LightLODLevel targetLOD;

                if (lightLOD.Priority == LightPriority.Critical)
                {
                    targetLOD = LightLODLevel.Full;
                }
                else if (distance < _fullLODDistance)
                {
                    targetLOD = LightLODLevel.Full;
                }
                else if (distance < _noShadowDistance)
                {
                    targetLOD = LightLODLevel.NoShadows;
                }
                else if (distance < _emissiveDistance)
                {
                    targetLOD = LightLODLevel.Emissive;
                }
                else if (distance < _billboardDistance)
                {
                    targetLOD = LightLODLevel.Billboard;
                }
                else
                {
                    targetLOD = LightLODLevel.Disabled;
                }

                lightLOD.SetLODLevel(targetLOD);
            }
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}
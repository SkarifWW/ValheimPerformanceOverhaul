using UnityEngine;
using System.Collections.Generic;

namespace ValheimPerformanceOverhaul.LightCulling
{
    public enum LightLODLevel
    {
        Full = 0,        // 0-20м: Полный свет с тенями
        NoShadows = 1,   // 20-40м: Свет без теней
        Emissive = 2,    // 40-70м: Только эмиссивный материал
        Billboard = 3,   // 70м+: Плоская текстура
        Disabled = 4     // Вне зоны видимости
    }

    public class LightLOD : MonoBehaviour
    {
        // Компоненты
        public Light LightSource { get; private set; }
        private MeshRenderer _meshRenderer;
        private Material _emissiveMaterial;
        private GameObject _billboardObject;
        private SpriteRenderer _billboardRenderer;

        // Оригинальные значения
        private float _originalIntensity;
        private float _originalRange;
        private LightShadows _originalShadows;
        private Material _originalMaterial;
        private Color _lightColor;

        // Текущее состояние
        private LightLODLevel _currentLOD = LightLODLevel.Full;
        public LightPriority Priority { get; private set; }

        // Кэшированные ресурсы (статические для экономии памяти)
        private static Texture2D _glowTexture;
        private static Material _billboardMaterial;

        private void Awake()
        {
            LightSource = GetComponent<Light>();
            if (LightSource == null) return;

            // Сохраняем оригинальные значения
            _originalIntensity = LightSource.intensity;
            _originalRange = LightSource.range;
            _originalShadows = LightSource.shadows;
            _lightColor = LightSource.color;

            // Пытаемся найти меш для эмиссии
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (_meshRenderer != null)
            {
                _originalMaterial = _meshRenderer.material;
            }

            DeterminePriority();
            InitializeResources();
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
            // Создаем общие ресурсы один раз для ВСЕХ источников света
            if (_glowTexture == null)
            {
                _glowTexture = CreateGlowTexture();
            }

            if (_billboardMaterial == null)
            {
                _billboardMaterial = CreateBillboardMaterial();
            }
        }

        private static Texture2D CreateGlowTexture()
        {
            // Создаем простую текстуру свечения 32x32
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    alpha = Mathf.Pow(alpha, 2f); // Плавный градиент
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            return texture;
        }

        private static Material CreateBillboardMaterial()
        {
            // Используем стандартный Unlit шейдер для максимальной производительности
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material mat = new Material(shader);
            mat.mainTexture = _glowTexture;
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", 0); // Double-sided
            mat.renderQueue = 3000; // Transparent queue

            return mat;
        }

        private void CreateBillboard()
        {
            if (_billboardObject != null) return;

            // Создаем quad для billboard
            _billboardObject = new GameObject("LightBillboard");
            _billboardObject.transform.SetParent(transform);
            _billboardObject.transform.localPosition = Vector3.zero;

            // Используем Sprite Renderer для автоматического billboard эффекта
            _billboardRenderer = _billboardObject.AddComponent<SpriteRenderer>();

            // Создаем спрайт из текстуры
            Sprite sprite = Sprite.Create(
                _glowTexture,
                new Rect(0, 0, _glowTexture.width, _glowTexture.height),
                new Vector2(0.5f, 0.5f)
            );

            _billboardRenderer.sprite = sprite;
            _billboardRenderer.material = _billboardMaterial;
            _billboardRenderer.color = _lightColor;

            // Масштаб зависит от яркости света
            float scale = Mathf.Lerp(0.5f, 2.0f, _originalIntensity / 3.0f);
            _billboardObject.transform.localScale = Vector3.one * scale;

            _billboardObject.SetActive(false);
        }

        private void CreateEmissiveMaterial()
        {
            if (_meshRenderer == null || _emissiveMaterial != null) return;

            // Клонируем оригинальный материал
            _emissiveMaterial = new Material(_originalMaterial);

            // Включаем emission
            _emissiveMaterial.EnableKeyword("_EMISSION");
            _emissiveMaterial.SetColor("_EmissionColor", _lightColor * _originalIntensity * 0.5f);

            // Если нет emission, пробуем добавить через основной цвет
            if (!_emissiveMaterial.HasProperty("_EmissionColor"))
            {
                _emissiveMaterial.SetColor("_Color", _lightColor * 1.5f);
            }
        }

        public void SetLODLevel(LightLODLevel level)
        {
            if (_currentLOD == level || LightSource == null) return;

            // Отключаем текущее состояние
            DisableCurrentLOD();

            _currentLOD = level;

            // Включаем новое состояние
            switch (level)
            {
                case LightLODLevel.Full:
                    EnableFullLight();
                    break;

                case LightLODLevel.NoShadows:
                    EnableLightNoShadows();
                    break;

                case LightLODLevel.Emissive:
                    EnableEmissive();
                    break;

                case LightLODLevel.Billboard:
                    EnableBillboard();
                    break;

                case LightLODLevel.Disabled:
                    // Все уже отключено в DisableCurrentLOD
                    break;
            }

            if (Plugin.DebugLoggingEnabled.Value)
            {
                Plugin.Log.LogInfo($"[LightLOD] {gameObject.name} switched to LOD {level}");
            }
        }

        private void DisableCurrentLOD()
        {
            // Отключаем Light
            if (LightSource != null)
            {
                LightSource.enabled = false;
            }

            // Отключаем эмиссию
            if (_meshRenderer != null && _originalMaterial != null)
            {
                _meshRenderer.material = _originalMaterial;
            }

            // Отключаем billboard
            if (_billboardObject != null)
            {
                _billboardObject.SetActive(false);
            }
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
            if (_meshRenderer != null)
            {
                if (_emissiveMaterial == null)
                {
                    CreateEmissiveMaterial();
                }

                if (_emissiveMaterial != null)
                {
                    _meshRenderer.material = _emissiveMaterial;
                }
            }
        }

        private void EnableBillboard()
        {
            if (_billboardObject == null)
            {
                CreateBillboard();
            }

            if (_billboardObject != null)
            {
                _billboardObject.SetActive(true);

                // Billboard всегда смотрит на камеру
                if (Camera.main != null)
                {
                    _billboardObject.transform.LookAt(Camera.main.transform);
                    _billboardObject.transform.Rotate(0, 180, 0); // Разворачиваем правильно
                }
            }
        }

        public LightLODLevel CurrentLOD => _currentLOD;

        private void OnDestroy()
        {
            // Очищаем созданные ресурсы
            if (_emissiveMaterial != null)
            {
                Destroy(_emissiveMaterial);
            }

            if (_billboardObject != null)
            {
                Destroy(_billboardObject);
            }
        }
    }

    // Менеджер для управления всеми LOD
    public class LightLODManager : MonoBehaviour
    {
        public static LightLODManager Instance { get; private set; }

        private readonly List<LightLOD> _allLights = new List<LightLOD>(512);
        private float _updateTimer;
        private const float UPDATE_INTERVAL = 0.5f; // Обновляем каждые 0.5 секунды

        // Настройки расстояний (можно вынести в конфиг)
        private float _fullLODDistance = 20f;
        private float _noShadowDistance = 40f;
        private float _emissiveDistance = 70f;
        private float _billboardDistance = 100f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            LoadConfig();
            ScanForLights();
            Plugin.Log.LogInfo($"[LightLOD] Initialized with {_allLights.Count} lights.");
        }

        private void LoadConfig()
        {
            // Загружаем из конфига, если добавите эти настройки
            _fullLODDistance = Plugin.LightLODFullDistance?.Value ?? 20f;
            _noShadowDistance = Plugin.LightLODNoShadowDistance?.Value ?? 40f;
            _emissiveDistance = Plugin.LightLODEmissiveDistance?.Value ?? 70f;
            _billboardDistance = Plugin.LightLODBillboardDistance?.Value ?? 100f;
        }

        private void ScanForLights()
        {
            Light[] lights = FindObjectsOfType<Light>(true);
            foreach (var light in lights)
            {
                RegisterLight(light);
            }
        }

        public void RegisterLight(Light light)
        {
            if (light == null || light.type == LightType.Directional) return;

            var lodComponent = light.GetComponent<LightLOD>();
            if (lodComponent == null)
            {
                lodComponent = light.gameObject.AddComponent<LightLOD>();
            }

            if (!_allLights.Contains(lodComponent))
            {
                _allLights.Add(lodComponent);
            }
        }

        private void Update()
        {
            if (Player.m_localPlayer == null || Camera.main == null) return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            UpdateLODs();
        }

        private void UpdateLODs()
        {
            Vector3 cameraPos = Camera.main.transform.position;

            _allLights.RemoveAll(l => l == null);

            foreach (var lightLOD in _allLights)
            {
                if (lightLOD == null || lightLOD.LightSource == null) continue;

                float distance = Vector3.Distance(cameraPos, lightLOD.transform.position);
                LightLODLevel targetLOD;

                // Критические источники (игрок) всегда Full LOD
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
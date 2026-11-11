using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Full-screen feedback for the player's two vitals, kept deliberately distinct so a glance tells
/// you which one is in trouble:
///
///   Low health  - red vignette closing in from the edges, pulsing like a heartbeat.
///   Low stamina - the player is winded: frosty pixel wisps at the screen edges that swell with each
///                 breath, and puffs of breath rising from the bottom. The middle of the screen and its
///                 colours stay untouched, so being out of stamina never makes the fight harder to read.
///
/// They stack: bleeding out while exhausted gives red edges with the wisps and breath on top.
///
/// The health vignette is built on a URP Volume rather than a custom shader. The pipeline already renders
/// vignette, so this only animates its parameters - no ShaderLab, no ScriptableRendererFeature to register
/// on both renderers, and no extra full-screen pass.
///
/// It creates its OWN Volume and profile at runtime rather than editing the level's Post Processing
/// profile. Writing to a Volume's sharedProfile at runtime edits the asset on disk in the Editor,
/// which would permanently bake these effects into the level's look.
///
/// The stamina effect is UI, placed right above the image the camera renders to, so the HUD and menus stay
/// on top of it. Its textures are drawn in code, in chunky pixels to match the low-res game image.
///
/// Bootstraps itself only if no instance was placed by hand, so it works in every level with zero
/// setup - drop the component on any GameObject if you want to tune the values in the Inspector.
/// </summary>
public class PlayerVitalsVfx : MonoBehaviour
{
    public static PlayerVitalsVfx Instance { get; private set; }

    [Header("Low Health - Red Vignette")]
    [Tooltip("Health fraction below which the red vignette starts. 0.35 = the bottom 35% of the bar.")]
    [Range(0f, 1f)] [SerializeField] private float healthThreshold = 0.35f;

    [SerializeField] private Color healthVignetteColor = new Color(0.5f, 0.02f, 0.03f);

    [Tooltip("Vignette intensity at zero health.")]
    [Range(0f, 1f)] [SerializeField] private float healthMaxIntensity = 0.5f;

    [Tooltip("Higher is a softer, wider falloff.")]
    [Range(0.01f, 1f)] [SerializeField] private float healthVignetteSmoothness = 0.4f;

    [Tooltip("Heartbeats per second at zero health.")]
    [SerializeField] private float healthPulseSpeed = 1.6f;

    [Tooltip("How far each beat dips. 0 = steady, 1 = fades to nothing on each beat.")]
    [Range(0f, 1f)] [SerializeField] private float healthPulseDepth = 0.4f;

    [Header("Low Stamina - Winded")]
    [Tooltip("Stamina fraction below which the effect starts. 0.25 = the bottom 25% of the bar.")]
    [Range(0f, 1f)] [SerializeField] private float staminaThreshold = 0.25f;

    [SerializeField] private Color staminaColor = new Color(0.68f, 0.86f, 0.96f);

    [Tooltip("Opacity of the edge wisps at zero stamina, at the top of a breath.")]
    [Range(0f, 1f)] [SerializeField] private float staminaEdgeAlpha = 0.5f;

    [Tooltip("Opacity of the breath puffs at zero stamina.")]
    [Range(0f, 1f)] [SerializeField] private float staminaPuffAlpha = 0.3f;

    [Tooltip("Seconds per breath.")]
    [SerializeField] private float staminaBreathSeconds = 1.1f;

    [Header("Response")]
    [Tooltip("How quickly each effect fades in and out as a bar crosses its threshold.")]
    [SerializeField] private float fadeSpeed = 6f;

    private Volume _volume;
    private VolumeProfile _profile;
    private Vignette _vignette;

    private const string GameImageTexture = "LowResFilter";
    private const int PuffCount = 6;
    private const float PuffSeconds = 1f;
    private const float PuffStep = 12f; // the puff texture's size: puffs grow in whole multiples, so their pixels stay crisp

    private static readonly float[] Dither =
    {
        0f / 16f,  8f / 16f,  2f / 16f, 10f / 16f,
        12f / 16f, 4f / 16f, 14f / 16f, 6f / 16f,
        3f / 16f, 11f / 16f, 1f / 16f,  9f / 16f,
        15f / 16f, 7f / 16f, 13f / 16f, 5f / 16f
    };

    private GameObject _windedRoot;
    private RawImage _edge;
    private readonly RawImage[] _puffs = new RawImage[PuffCount];
    private readonly float[] _puffAge = new float[PuffCount];
    private readonly float[] _puffDrift = new float[PuffCount];
    private Texture2D _edgeTexture;
    private Texture2D _puffTexture;
    private float _breathTimer;
    private float _heartbeatTimer;
    private float _windedRetryTimer;

    private PlayerHealth _health;
    private PlayerStamina _stamina;
    private float _retryTimer;

    private float _healthStrength;
    private float _staminaStrength;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        // Respect a hand-placed instance so its Inspector values win.
        if (FindFirstObjectByType<PlayerVitalsVfx>(FindObjectsInactive.Include) != null) return;

        GameObject host = new GameObject("[PlayerVitalsVfx]");
        host.AddComponent<PlayerVitalsVfx>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // The profile is a runtime-only object, so it has to be cleaned up by hand.
        if (_profile != null) Destroy(_profile);
        if (_edgeTexture != null) Destroy(_edgeTexture);
        if (_puffTexture != null) Destroy(_puffTexture);
    }

    private void BuildVolume()
    {
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.hideFlags = HideFlags.HideAndDontSave;

        _vignette = _profile.Add<Vignette>(false);
        _vignette.color.overrideState = true;
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;
        _vignette.color.value = healthVignetteColor;
        _vignette.intensity.value = 0f;
        _vignette.smoothness.value = healthVignetteSmoothness;

        _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 100f; // Above the level's own profile so it always layers on top.
        _volume.weight = 0f;
        _volume.profile = _profile;
    }

    private void Update()
    {
        if (_volume == null) return;

        EnsurePlayerRefs();

        // Scaled time on purpose: the effects hold steady while the game is paused rather than
        // pulsing away behind a menu.
        float delta = fadeSpeed * Time.deltaTime;
        _healthStrength = Mathf.MoveTowards(_healthStrength, ResolveHealthStrength(), delta);
        _staminaStrength = Mathf.MoveTowards(_staminaStrength, ResolveStaminaStrength(), delta);

        ApplyWinded();

        bool healthActive = _healthStrength > 0.001f;
        _volume.weight = healthActive ? 1f : 0f;
        if (healthActive) ApplyHealthVignette();
    }

    private void ApplyHealthVignette()
    {
        _vignette.color.value = healthVignetteColor;
        _vignette.smoothness.value = healthVignetteSmoothness;

        if (_healthStrength <= 0.001f)
        {
            _vignette.intensity.value = 0f;
            return;
        }

        // Beats faster and deeper the worse it gets.
        float phase = (Mathf.Sin(Time.time * healthPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float pulse = Mathf.Lerp(1f - (healthPulseDepth * _healthStrength), 1f, phase);

        _vignette.intensity.value = _healthStrength * healthMaxIntensity * pulse;

        _heartbeatTimer -= Time.deltaTime;
        if (_heartbeatTimer <= 0f && _health != null && _health.CurrentHealth > 0f)
        {
            _heartbeatTimer = 1.1f;
            FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Heartbeat");
        }
    }

    private void ApplyWinded()
    {
        bool active = _staminaStrength > 0.001f;
        if (!active)
        {
            if (_windedRoot != null && _windedRoot.activeSelf) _windedRoot.SetActive(false);
            return;
        }

        if (_windedRoot == null && !TryBuildWinded()) return;
        if (!_windedRoot.activeSelf)
        {
            _windedRoot.SetActive(true);
            _breathTimer = 0f;
        }

        // A slow breath rather than a heartbeat, so it stays separate from low health when both are on screen.
        bool still = GauntletScreenFx.ReduceMotion;
        float breath = still ? 1f : (Mathf.Sin(Time.time * Mathf.PI * 2f / staminaBreathSeconds) + 1f) * 0.5f;

        Color edge = staminaColor;
        edge.a = _staminaStrength * staminaEdgeAlpha * Mathf.Lerp(0.6f, 1f, breath);
        _edge.color = edge;

        _breathTimer -= Time.deltaTime;
        if (_breathTimer <= 0f && _health != null && _health.CurrentHealth > 0f)
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Winded");
            if (still) _breathTimer = staminaBreathSeconds; // with Reduce Motion the puffs below never reset it
        }

        if (!still && _breathTimer <= 0f)
        {
            _breathTimer = staminaBreathSeconds;
            SpawnPuff();
            SpawnPuff();
        }

        for (int i = 0; i < PuffCount; i++) UpdatePuff(i);
    }

    private void SpawnPuff()
    {
        for (int i = 0; i < PuffCount; i++)
        {
            if (_puffs[i].gameObject.activeSelf) continue;

            _puffs[i].gameObject.SetActive(true);
            _puffAge[i] = 0f;
            _puffDrift[i] = Random.Range(-1f, 1f);
            return;
        }
    }

    private void UpdatePuff(int i)
    {
        RawImage puff = _puffs[i];
        if (!puff.gameObject.activeSelf) return;

        _puffAge[i] += Time.deltaTime;
        float t = _puffAge[i] / PuffSeconds;
        if (t >= 1f)
        {
            puff.gameObject.SetActive(false);
            return;
        }

        float size = Mathf.Round(Mathf.Lerp(48f, 120f, t) / PuffStep) * PuffStep;
        puff.rectTransform.sizeDelta = new Vector2(size, size);
        puff.rectTransform.anchoredPosition = new Vector2(_puffDrift[i] * Mathf.Lerp(60f, 170f, t), Mathf.Lerp(0f, 150f, t));

        Color color = staminaColor;
        color.a = _staminaStrength * staminaPuffAlpha * Mathf.Sin(t * Mathf.PI);
        puff.color = color;
    }

    /// <summary>
    /// The canvas belongs to the level while this object persists, so the UI is rebuilt per level, the first
    /// time it is needed. Throttled for scenes with no game image to sit on.
    /// </summary>
    private bool TryBuildWinded()
    {
        _windedRetryTimer -= Time.unscaledDeltaTime;
        if (_windedRetryTimer > 0f) return false;
        _windedRetryTimer = 0.5f;

        RawImage gameImage = null;
        foreach (RawImage raw in FindObjectsByType<RawImage>(FindObjectsSortMode.None))
        {
            // A prefix, not the whole name: LowResScreen swaps in a copy sized to the window, "LowResFilter (fitted)".
            if (raw.texture != null && raw.texture.name.StartsWith(GameImageTexture)) gameImage = raw;
        }
        if (gameImage == null) return false;

        if (_edgeTexture == null) _edgeTexture = BuildEdgeTexture();
        if (_puffTexture == null) _puffTexture = BuildPuffTexture();

        _windedRoot = new GameObject("[WindedFx]", typeof(RectTransform));
        RectTransform root = (RectTransform)_windedRoot.transform;
        root.SetParent(gameImage.transform.parent, false);
        root.SetSiblingIndex(gameImage.transform.GetSiblingIndex() + 1);
        Stretch(root);

        _edge = NewLayer("Edge", root, _edgeTexture);
        Stretch(_edge.rectTransform);

        for (int i = 0; i < PuffCount; i++)
        {
            _puffs[i] = NewLayer("Puff", root, _puffTexture);
            _puffs[i].rectTransform.anchorMin = _puffs[i].rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _puffs[i].gameObject.SetActive(false);
        }

        return true;
    }

    private static RawImage NewLayer(string layerName, Transform parent, Texture texture)
    {
        GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(RawImage));
        layer.transform.SetParent(parent, false);

        RawImage image = layer.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Three opacity levels with an ordered dither between them: the look of hand-placed pixels, not a soft gradient.
    private static Color32 PixelAt(int x, int y, float alpha)
    {
        float level = Mathf.Floor(Mathf.Clamp01(alpha) * 3f + Dither[(y % 4) * 4 + x % 4]);
        return new Color32(255, 255, 255, (byte)(Mathf.Min(level, 3f) / 3f * 255f));
    }

    private static Texture2D NewPixelTexture(int width, int height, Color32[] pixels)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D BuildEdgeTexture()
    {
        const int width = 240;
        const int height = 135;
        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));

                // Noise on the reach is what turns a plain frame into uneven wisps. It stays a thin band: anything
                // wider starts covering what the player needs to see.
                float reach = Mathf.Lerp(2f, 11f, Mathf.PerlinNoise(x * 0.11f, y * 0.11f));
                pixels[y * width + x] = PixelAt(x, y, 1f - distance / reach);
            }
        }

        return NewPixelTexture(width, height, pixels);
    }

    private static Texture2D BuildPuffTexture()
    {
        int size = (int)PuffStep;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                pixels[y * size + x] = PixelAt(x, y, 1f - distance / (size * 0.5f));
            }
        }

        return NewPixelTexture(size, size, pixels);
    }

    /// <summary>0 when health is comfortable, ramping to 1 when it is empty.</summary>
    private float ResolveHealthStrength()
    {
        if (_health == null || _health.MaxHealth <= 0f) return 0f;

        float fraction = _health.CurrentHealth / _health.MaxHealth;
        if (fraction >= healthThreshold) return 0f;

        return Mathf.InverseLerp(healthThreshold, 0f, fraction);
    }

    /// <summary>0 when stamina is comfortable, ramping to 1 when it is empty.</summary>
    private float ResolveStaminaStrength()
    {
        if (_stamina == null || _stamina.MaxStamina <= 0f) return 0f;

        float fraction = _stamina.CurrentStamina / _stamina.MaxStamina;
        if (fraction >= staminaThreshold) return 0f;

        return Mathf.InverseLerp(staminaThreshold, 0f, fraction);
    }

    /// <summary>
    /// The player is spawned per level while this object persists, so the references have to be
    /// re-found. Throttled because on a menu scene there is nothing to find and an unthrottled
    /// search would run every frame.
    /// </summary>
    private void EnsurePlayerRefs()
    {
        if (_health != null && _stamina != null) return;

        _retryTimer -= Time.unscaledDeltaTime;
        if (_retryTimer > 0f) return;

        _retryTimer = 0.5f;

        if (_health == null) _health = FindFirstObjectByType<PlayerHealth>();

        // Both live on the player, so prefer a direct lookup over a second scene-wide search.
        if (_stamina == null && _health != null) _stamina = _health.GetComponent<PlayerStamina>();
        if (_stamina == null) _stamina = FindFirstObjectByType<PlayerStamina>();
    }
}

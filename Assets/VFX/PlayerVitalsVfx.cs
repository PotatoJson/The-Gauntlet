using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Full-screen feedback for the player's two vitals, kept deliberately distinct so a glance tells
/// you which one is in trouble:
///
///   Low health  - red vignette closing in from the edges, pulsing like a heartbeat.
///   Low stamina - colour drains out of the world towards greyscale, steady rather than pulsing.
///
/// They stack: bleeding out while exhausted gives a desaturated screen with red edges.
///
/// Built on a URP Volume rather than a custom shader. The pipeline already renders vignette and
/// colour grading, so this only animates their parameters - no ShaderLab, no ScriptableRendererFeature
/// to register on both renderers, and no extra full-screen pass.
///
/// It creates its OWN Volume and profile at runtime rather than editing the level's Post Processing
/// profile. Writing to a Volume's sharedProfile at runtime edits the asset on disk in the Editor,
/// which would permanently bake these effects into the level's look.
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

    [Header("Low Stamina - Desaturation")]
    [Tooltip("Stamina fraction below which colour starts draining. 0.4 = the bottom 40% of the bar.")]
    [Range(0f, 1f)] [SerializeField] private float staminaThreshold = 0.4f;

    [Tooltip("How grey the screen goes when stamina is empty. 100 = fully greyscale.")]
    [Range(0f, 100f)] [SerializeField] private float staminaMaxDesaturation = 95f;

    [Tooltip("Darkening as stamina empties, in EXPOSURE STOPS - not a percentage. Each whole " +
             "step halves the screen brightness, so -1 is already very dark. Keep this within a " +
             "fraction of a stop, or set it to 0 for pure greyscale with no dimming.")]
    [Range(-1f, 0f)] [SerializeField] private float staminaExposureDrop = -0.15f;

    [Header("Response")]
    [Tooltip("How quickly each effect fades in and out as a bar crosses its threshold.")]
    [SerializeField] private float fadeSpeed = 6f;

    private Volume _volume;
    private VolumeProfile _profile;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;

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

        _colorAdjustments = _profile.Add<ColorAdjustments>(false);
        _colorAdjustments.saturation.overrideState = true;
        _colorAdjustments.postExposure.overrideState = true;
        _colorAdjustments.saturation.value = 0f;
        _colorAdjustments.postExposure.value = 0f;

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

        bool anyActive = _healthStrength > 0.001f || _staminaStrength > 0.001f;
        _volume.weight = anyActive ? 1f : 0f;
        if (!anyActive) return;

        ApplyHealthVignette();
        ApplyStaminaDesaturation();
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
    }

    private void ApplyStaminaDesaturation()
    {
        // No pulse here. Exhaustion reads as the world going flat and grey, which stays clearly
        // separate from the heartbeat of low health even when both are on screen at once.
        _colorAdjustments.saturation.value = -_staminaStrength * staminaMaxDesaturation;
        _colorAdjustments.postExposure.value = _staminaStrength * staminaExposureDrop;
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

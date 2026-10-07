using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A very short, softened inverted frame, used for the fire gauntlet's skill. The game is rendered to
/// a low-res texture shown on a RawImage under the HUD, so swapping that RawImage's material changes the
/// world only and leaves the HUD untouched. Runs on unscaled time.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ImpactFrame : MonoBehaviour
{
    public const string PrefKey = "ImpactFrames"; // 1 (default) = on, 0 = off, for a future settings toggle

    public static ImpactFrame Instance { get; private set; }

    [SerializeField] private Material impactMaterial;

    [Header("Look")]
    [Tooltip("Roughly what share of the screen ends up 'light' before inverting. The cut-off is measured from the " +
             "current image each time, so dark dungeons and bright rooms both come out readable.")]
    [SerializeField, Range(0.05f, 0.95f)] private float lightFraction = 0.35f;
    [Tooltip("0 = normal image, 1 = full two-tone. Lower is less contrasty.")]
    [SerializeField, Range(0f, 1f)] private float strength = 0.5f;
    [SerializeField] private Color lightColor = new Color(1f, 0.82f, 0.5f, 1f);
    [SerializeField] private Color darkColor = new Color(0.1f, 0.03f, 0.02f, 1f);

    [Header("Timing (seconds, unscaled)")]
    [Tooltip("About two frames at 60 fps.")]
    [SerializeField] private float duration = 0.035f;
    [SerializeField] private float minInterval = 0.3f;

    private RawImage _target;
    private Material _runtimeMat;
    private Material _originalMat;
    private Coroutine _routine;
    private Texture2D _fullTex;
    private float[] _lums = new float[(1920 / 8 + 1) * (1080 / 8 + 1)];
    private float _lastPlayTime = -10f;

    public static bool Enabled => PlayerPrefs.GetInt(PrefKey, 1) == 1;

    /// <summary>Safe to call from anywhere; does nothing if no ImpactFrame exists in the scene.</summary>
    public static void Play()
    {
        if (Instance != null) Instance.Run();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        _target = GetComponent<RawImage>();
        _originalMat = _target.material;
        if (impactMaterial != null) _runtimeMat = new Material(impactMaterial);
    }

    private void OnDisable()
    {
        Restore();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_runtimeMat != null) Destroy(_runtimeMat);
        if (_fullTex != null) Destroy(_fullTex);
    }

    private void Run()
    {
        if (_runtimeMat == null || !Enabled || !isActiveAndEnabled) return;
        if (Time.unscaledTime - _lastPlayTime < minInterval) return;
        _lastPlayTime = Time.unscaledTime;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        _runtimeMat.SetFloat("_Threshold", MeasureThreshold());
        _runtimeMat.SetFloat("_Invert", 1f);
        _runtimeMat.SetFloat("_Mix", strength);
        _runtimeMat.SetColor("_LightColor", lightColor);
        _runtimeMat.SetColor("_DarkColor", darkColor);

        _target.material = _runtimeMat;
        yield return new WaitForSecondsRealtime(duration);
        Restore();
    }

    // Reads the game image back and returns the perceptual brightness that splits it by lightFraction.
    // Samples every 8th pixel straight from the raw bytes, so there is no big array to allocate.
    private float MeasureThreshold()
    {
        RenderTexture rt = _target.texture as RenderTexture;
        if (rt == null) return 0.2f;

        if (_fullTex == null || _fullTex.width != rt.width || _fullTex.height != rt.height)
        {
            if (_fullTex != null) Destroy(_fullTex);
            _fullTex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true); // linear: raw RT values
        }

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        _fullTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
        RenderTexture.active = prev;

        Unity.Collections.NativeArray<byte> data = _fullTex.GetRawTextureData<byte>();
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        const int step = 8;
        int n = 0;
        for (int y = 0; y < rt.height; y += step)
        {
            for (int x = 0; x < rt.width; x += step)
            {
                int i = (y * rt.width + x) * 4;
                float lum = (0.299f * data[i] + 0.587f * data[i + 1] + 0.114f * data[i + 2]) / 255f;
                if (n >= _lums.Length) break;
                _lums[n++] = linear ? Mathf.Pow(lum, 0.4545f) : lum;
            }
        }

        System.Array.Sort(_lums, 0, n);
        float cut = _lums[Mathf.Clamp(Mathf.RoundToInt((1f - lightFraction) * (n - 1)), 0, n - 1)];
        return Mathf.Clamp(cut, 0.04f, 0.7f);
    }

    private void Restore()
    {
        _routine = null;
        if (_target != null) _target.material = _originalMat;
    }
}

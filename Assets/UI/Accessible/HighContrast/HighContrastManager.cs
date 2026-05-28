using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HighContrastManager : MonoBehaviour
{
    public static HighContrastManager Instance; // Fixes CS0117

    [Header("URP Renderer Features")]
    [SerializeField] private UniversalRendererData rendererData;
    [SerializeField] private Material playerOutlineMat;
    [SerializeField] private Material enemyOutlineMat;
    [SerializeField] private Material structureOutlineMat;

    [Header("UI References")]
    [SerializeField] private Toggle hcToggle;
    [SerializeField] private Toggle grayscaleToggle;

    [Header("Post Processing")]
    [SerializeField] private VolumeProfile globalProfile;

    private static bool _isEnabled = false; // Fixes CS0103
    private static bool _grayscaleEnabled = false;
    private static Color _pColor = Color.green;
    private static Color _eColor = Color.red;
    private static Color _sColor = Color.black;

    private ScriptableRendererFeature _pFeature;
    private ScriptableRendererFeature _eFeature;
    private ScriptableRendererFeature _sFeature;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Find the features in your Renderer Data asset
        _pFeature = rendererData.rendererFeatures.Find(f => f.name == "PlayerOutline");
        _eFeature = rendererData.rendererFeatures.Find(f => f.name == "EnemyOutline");
        _sFeature = rendererData.rendererFeatures.Find(f => f.name == "StructureOutline");

        UpdateFeatureStates();
    }

    private void OnEnable()
    {
        if (hcToggle != null) hcToggle.isOn = _isEnabled;
        if (grayscaleToggle != null) grayscaleToggle.isOn = _grayscaleEnabled;
        UpdateFeatureStates();
        UpdateGrayscale();
    }

    private void Start()
    {
        UpdateAllMaterialColors();
    }

    private void OnDestroy()
    {
        // Safety: Turn features off when closing Unity to avoid Editor bugs
        if (_pFeature != null) _pFeature.SetActive(false);
        if (_eFeature != null) _eFeature.SetActive(false);
        if (_sFeature != null) _sFeature.SetActive(false);
    }

    public void ToggleHighContrast(bool isOn)
    {
        _isEnabled = isOn;
        UpdateFeatureStates();
    }

    public void ToggleGrayscale(bool isOn)
    {
        _grayscaleEnabled = isOn;
        UpdateGrayscale();
    }

    private void UpdateFeatureStates()
    {
        if (_pFeature != null) _pFeature.SetActive(_isEnabled);
        if (_eFeature != null) _eFeature.SetActive(_isEnabled);
        if (_sFeature != null) _sFeature.SetActive(_isEnabled);
    }

    private void UpdateGrayscale()
    {
        if (globalProfile == null) return;
        if (globalProfile.TryGet<ColorAdjustments>(out var ca))
        {
            ca.saturation.overrideState = true;
            ca.saturation.value = _grayscaleEnabled ? -100f : 0f;
        }
    }

    private void UpdateAllMaterialColors()
    {
        if (playerOutlineMat != null) playerOutlineMat.SetColor("_Color", _pColor);
        if (enemyOutlineMat != null) enemyOutlineMat.SetColor("_Color", _eColor);
        if (structureOutlineMat != null) structureOutlineMat.SetColor("_Color", _sColor);
    }
}
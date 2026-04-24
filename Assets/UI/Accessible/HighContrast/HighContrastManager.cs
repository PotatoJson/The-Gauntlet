using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private FlexibleColorPicker fcp;
    [SerializeField] private Toggle hcToggle;
    [SerializeField] private GameObject targetButtonParent; // Fixes CS0103
    [SerializeField] private GameObject pickerAndPreviewGroup;

    [Header("Preview Models")]
    [SerializeField] private GameObject playerPreview;
    [SerializeField] private GameObject enemyPreview;
    [SerializeField] private GameObject structurePreview;

    private enum EditingTarget { Player, Enemy, Structure }
    private EditingTarget _currentTarget = EditingTarget.Player;

    private static bool _isEnabled = false; // Fixes CS0103
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
        if (targetButtonParent != null) targetButtonParent.SetActive(_isEnabled);
        UpdateFeatureStates();
    }

    private void Start()
    {
        if (fcp != null) fcp.onColorChange.AddListener(OnPickerColorChanged);
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
        if (targetButtonParent != null) targetButtonParent.SetActive(isOn);

        // Hide the color picker group if the main toggle is turned off
        if (!isOn && pickerAndPreviewGroup != null) pickerAndPreviewGroup.SetActive(false);

        UpdateFeatureStates();
    }

    private void UpdateFeatureStates()
    {
        if (_pFeature != null) _pFeature.SetActive(_isEnabled);
        if (_eFeature != null) _eFeature.SetActive(_isEnabled);
        if (_sFeature != null) _sFeature.SetActive(_isEnabled);
    }

    public void SelectTarget(int index)
    {
        _currentTarget = (EditingTarget)index;
        if (pickerAndPreviewGroup != null) pickerAndPreviewGroup.SetActive(true);

        SyncPickerToTarget();
        UpdatePreviewVisibility();
    }

    private void OnPickerColorChanged(Color newColor)
    {
        switch (_currentTarget)
        {
            case EditingTarget.Player:
                _pColor = newColor;
                if (playerOutlineMat != null) playerOutlineMat.SetColor("_Color", newColor); // Matches Shader Graph property
                break;
            case EditingTarget.Enemy:
                _eColor = newColor;
                if (enemyOutlineMat != null) enemyOutlineMat.SetColor("_Color", newColor);
                break;
            case EditingTarget.Structure:
                _sColor = newColor;
                if (structureOutlineMat != null) structureOutlineMat.SetColor("_Color", newColor);
                break;
        }
    }

    private void UpdatePreviewVisibility()
    {
        // Toggle the specific 3D model in your UI menu
        if (playerPreview != null) playerPreview.SetActive(_currentTarget == EditingTarget.Player);
        if (enemyPreview != null) enemyPreview.SetActive(_currentTarget == EditingTarget.Enemy);
        if (structurePreview != null) structurePreview.SetActive(_currentTarget == EditingTarget.Structure);
    }

    private void UpdateAllMaterialColors()
    {
        if (playerOutlineMat != null) playerOutlineMat.SetColor("_Color", _pColor);
        if (enemyOutlineMat != null) enemyOutlineMat.SetColor("_Color", _eColor);
        if (structureOutlineMat != null) structureOutlineMat.SetColor("_Color", _sColor);
    }

    private void SyncPickerToTarget()
    {
        if (fcp == null) return;
        fcp.color = _currentTarget switch
        {
            EditingTarget.Player => _pColor,
            EditingTarget.Enemy => _eColor,
            _ => _sColor
        };
    }
}
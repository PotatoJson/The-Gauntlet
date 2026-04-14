using UnityEngine;
using UnityEngine.UI;

public class HighContrastManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlexibleColorPicker fcp;
    [SerializeField] private Material hcMaterial;
    [SerializeField] private Toggle hcToggle;

    [Header("UI Groups")]
    [SerializeField] private GameObject targetButtonParent;
    [SerializeField] private GameObject pickerAndPreviewGroup;

    [Header("Preview Models")]
    [SerializeField] private HighContrastTarget playerPreview;
    [SerializeField] private HighContrastTarget enemyPreview;
    [SerializeField] private HighContrastTarget structurePreview;

    private enum EditingTarget { Player, Enemy, Structure }
    private EditingTarget _currentTarget = EditingTarget.Player;

    // Static variables to keep memory between opening/closing the menu
    private static bool _isEnabled = false;
    private static Color _pColor = Color.green;
    private static Color _eColor = Color.red;
    private static Color _sColor = Color.black;

    private void OnEnable()
    {
        // 1. Sync the Toggle UI to our saved state
        if (hcToggle != null) hcToggle.isOn = _isEnabled;

        // 2. Show/Hide groups based on current state
        targetButtonParent.SetActive(_isEnabled);
        if (!_isEnabled) pickerAndPreviewGroup.SetActive(false);

        // 3. Immediately force the picker and 3D models to match our SAVED colors
        SyncPickerToTarget();
        UpdatePreviewModelColors();
    }

    private void Start()
    {
        // Add listener for when the PLAYER moves the slider
        fcp.onColorChange.AddListener(OnPickerColorChanged);
    }

    public void ToggleHighContrast(bool isOn)
    {
        _isEnabled = isOn;
        targetButtonParent.SetActive(isOn);
        if (!isOn) pickerAndPreviewGroup.SetActive(false);

        RefreshScene();
    }

    public void SelectTarget(int index)
    {
        _currentTarget = (EditingTarget)index;
        pickerAndPreviewGroup.SetActive(true);

        SyncPickerToTarget();
        UpdatePreviewVisibility();
    }

    private void SyncPickerToTarget()
    {
        // Get the color we actually saved in our static variables
        Color savedColor = _currentTarget switch
        {
            EditingTarget.Player => _pColor,
            EditingTarget.Enemy => _eColor,
            _ => _sColor
        };

        // Set the picker color directly (this won't trigger an infinite loop now)
        fcp.color = savedColor;
    }

    private void OnPickerColorChanged(Color newColor)
    {
        // Update our permanent storage
        switch (_currentTarget)
        {
            case EditingTarget.Player: _pColor = newColor; break;
            case EditingTarget.Enemy: _eColor = newColor; break;
            case EditingTarget.Structure: _sColor = newColor; break;
        }

        // Apply to the 3D preview model on the right
        HighContrastTarget current = GetCurrentPreview();
        if (current != null) current.ApplyColor(hcMaterial, newColor);

        // Update the actual game world if mode is enabled
        if (_isEnabled) RefreshScene();
    }

    private void UpdatePreviewVisibility()
    {
        if (playerPreview != null) playerPreview.gameObject.SetActive(_currentTarget == EditingTarget.Player);
        if (enemyPreview != null) enemyPreview.gameObject.SetActive(_currentTarget == EditingTarget.Enemy);
        if (structurePreview != null) structurePreview.gameObject.SetActive(_currentTarget == EditingTarget.Structure);
        UpdatePreviewModelColors();
    }

    private void UpdatePreviewModelColors()
    {
        // Ensure the 3D models match our static colors exactly
        if (playerPreview != null) playerPreview.ApplyColor(hcMaterial, _pColor);
        if (enemyPreview != null) enemyPreview.ApplyColor(hcMaterial, _eColor);
        if (structurePreview != null) structurePreview.ApplyColor(hcMaterial, _sColor);
    }

    private HighContrastTarget GetCurrentPreview() => _currentTarget switch
    {
        EditingTarget.Player => playerPreview,
        EditingTarget.Enemy => enemyPreview,
        _ => structurePreview
    };

    public void RefreshScene()
    {
        HighContrastTarget[] targets = FindObjectsOfType<HighContrastTarget>();
        foreach (var t in targets)
        {
            // IMPORTANT: Don't let the global scene refresh mess with our UI preview models
            if (t == playerPreview || t == enemyPreview || t == structurePreview) continue;

            if (!_isEnabled) { t.ResetMaterials(); continue; }

            if (t.type == HighContrastTarget.TargetType.Player) t.ApplyColor(hcMaterial, _pColor);
            else if (t.type == HighContrastTarget.TargetType.Enemy) t.ApplyColor(hcMaterial, _eColor);
            else if (t.type == HighContrastTarget.TargetType.Structure) t.ApplyColor(hcMaterial, _sColor);
        }
    }
}
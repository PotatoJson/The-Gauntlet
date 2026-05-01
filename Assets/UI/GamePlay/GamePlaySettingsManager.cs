using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for the text labels

public class GameplaySettingsManager : MonoBehaviour
{
    [Header("Mouse Settings")]
    [SerializeField] private Slider mouseSlider;
    [SerializeField] private TextMeshProUGUI mouseValueText;

    [Header("Controller Settings")]
    [SerializeField] private Slider controllerSlider;
    [SerializeField] private TextMeshProUGUI controllerValueText;

    void Start()
    {
        // 1. Load saved values (matching PlayerCamera defaults)
        float savedMouse = PlayerPrefs.GetFloat("MouseSensitivity", 0.02f);
        float savedController = PlayerPrefs.GetFloat("ControllerSensitivity", 1f);

        // 2. Initialize UI
        mouseSlider.value = savedMouse;
        controllerSlider.value = savedController;

        UpdateMouseText(savedMouse);
        UpdateControllerText(savedController);

        // 3. Apply to Camera
        ApplyMouseSensitivity(savedMouse);
        ApplyControllerSensitivity(savedController);

        // 4. Set up listeners for real-time updates
        mouseSlider.onValueChanged.AddListener(value => {
            UpdateMouseText(value);
            ApplyMouseSensitivity(value);
        });

        controllerSlider.onValueChanged.AddListener(value => {
            UpdateControllerText(value);
            ApplyControllerSensitivity(value);
        });
    }

    private void UpdateMouseText(float value)
    {
        // Mouse sensitivity uses small increments (0.001), so show 3 decimal places
        if (mouseValueText != null) mouseValueText.text = value.ToString("F3");
    }

    private void UpdateControllerText(float value)
    {
        // Controller sensitivity is a larger multiplier, show 1 decimal place
        if (controllerValueText != null) controllerValueText.text = value.ToString("F1");
    }

    public void ApplyMouseSensitivity(float value)
    {
        if (PlayerCamera.Instance != null)
        {
            PlayerCamera.Instance.SetMouseSensitivity(value);
            PlayerPrefs.SetFloat("MouseSensitivity", value);
        }
    }

    public void ApplyControllerSensitivity(float value)
    {
        if (PlayerCamera.Instance != null)
        {
            PlayerCamera.Instance.controllerSensitivityMultiplier = value;
            PlayerPrefs.SetFloat("ControllerSensitivity", value);
        }
    }
}
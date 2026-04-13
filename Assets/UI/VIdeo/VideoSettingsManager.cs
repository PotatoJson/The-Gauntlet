using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VideoSettingsManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown fpsDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private Resolution[] _resolutions;

    void Start()
    {
        SetupResolution();
        SetupFPS();

        // Setup Fullscreen Toggle
        fullscreenToggle.isOn = Screen.fullScreen;
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    private void SetupResolution()
    {
        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            string option = _resolutions[i].width + " x " + _resolutions[i].height;

            // Avoid adding duplicates (Unity lists resolutions for every refresh rate)
            if (!options.Contains(option))
            {
                options.Add(option);
            }

            if (_resolutions[i].width == Screen.currentResolution.width &&
                _resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = options.Count - 1;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    private void SetupFPS()
    {
        fpsDropdown.ClearOptions();
        List<string> fpsOptions = new List<string> { "30 FPS", "60 FPS", "120 FPS", "Unlimited" };
        fpsDropdown.AddOptions(fpsOptions);

        // Load saved FPS or default to 60
        int savedFPS = PlayerPrefs.GetInt("TargetFPS", 1); // Index 1 is 60 FPS
        fpsDropdown.value = savedFPS;
        SetFPS(savedFPS);

        fpsDropdown.onValueChanged.AddListener(SetFPS);
    }

    public void SetResolution(int resolutionIndex)
    {
        // Find the matching resolution from the array
        string[] splitRes = resolutionDropdown.options[resolutionIndex].text.Split('x');
        int width = int.Parse(splitRes[0].Trim());
        int height = int.Parse(splitRes[1].Trim());

        Screen.SetResolution(width, height, Screen.fullScreen);
    }

    public void SetFPS(int index)
    {
        int target = index switch
        {
            0 => 30,
            1 => 60,
            2 => 120,
            _ => -1 // -1 tells Unity to run as fast as possible
        };

        Application.targetFrameRate = target;
        PlayerPrefs.SetInt("TargetFPS", index);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
}
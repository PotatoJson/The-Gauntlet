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
        // Load saved Fullscreen preference (Default to true/1 if no save exists)
        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        fullscreenToggle.isOn = isFullscreen;
        Screen.fullScreen = isFullscreen;

        SetupResolution();
        SetupFPS();

        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    private void SetupResolution()
    {
        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        // Load saved resolution preferences (Default to Screen.width/height if no save exists)
        int savedWidth = PlayerPrefs.GetInt("ResWidth", Screen.width);
        int savedHeight = PlayerPrefs.GetInt("ResHeight", Screen.height);

        for (int i = 0; i < _resolutions.Length; i++)
        {
            string option = _resolutions[i].width + " x " + _resolutions[i].height;

            if (!options.Contains(option))
            {
                options.Add(option);
            }

            // THE FIX: Check against the saved width/height (or the actual game window size), 
            // NOT the monitor's native Screen.currentResolution!
            if (_resolutions[i].width == savedWidth && _resolutions[i].height == savedHeight)
            {
                currentResolutionIndex = options.Count - 1;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // Apply the loaded resolution just to be sure it matches the dropdown
        Screen.SetResolution(savedWidth, savedHeight, Screen.fullScreen);

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    private void SetupFPS()
    {
        fpsDropdown.ClearOptions();
        List<string> fpsOptions = new List<string> { "30 FPS", "60 FPS", "120 FPS", "Unlimited" };
        fpsDropdown.AddOptions(fpsOptions);

        int savedFPS = PlayerPrefs.GetInt("TargetFPS", 1);
        fpsDropdown.value = savedFPS;
        SetFPS(savedFPS);

        fpsDropdown.onValueChanged.AddListener(SetFPS);
    }

    public void SetResolution(int resolutionIndex)
    {
        string[] splitRes = resolutionDropdown.options[resolutionIndex].text.Split('x');
        int width = int.Parse(splitRes[0].Trim());
        int height = int.Parse(splitRes[1].Trim());

        Screen.SetResolution(width, height, Screen.fullScreen);

        // Save the player's choice!
        PlayerPrefs.SetInt("ResWidth", width);
        PlayerPrefs.SetInt("ResHeight", height);
        PlayerPrefs.Save();
    }

    public void SetFPS(int index)
    {
        int target = index switch
        {
            0 => 30,
            1 => 60,
            2 => 120,
            _ => -1
        };

        Application.targetFrameRate = target;
        PlayerPrefs.SetInt("TargetFPS", index);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;

        // Save the player's choice! (1 for true, 0 for false)
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }
}
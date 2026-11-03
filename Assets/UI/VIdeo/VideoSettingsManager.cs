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
    [SerializeField] private Toggle showFpsToggle;

    [Header("FPS Display")]
    [SerializeField] private GameObject fpsDisplay;

    private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
    private Vector2Int _resolution;
    private bool _fullscreen;

    void Start()
    {
        // Load saved Fullscreen preference (Default to true/1 if no save exists)
        _fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        fullscreenToggle.isOn = _fullscreen;

        // Load saved Show FPS preference (Default to false/0)
        bool showFps = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
        if (showFpsToggle != null)
        {
            showFpsToggle.isOn = showFps;
            showFpsToggle.onValueChanged.AddListener(SetShowFPS);
        }
        SetShowFPS(showFps);

        SetupResolution();
        SetupFPS();

        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    public void SetShowFPS(bool value)
    {
        if (fpsDisplay != null)
        {
            fpsDisplay.SetActive(value);
        }
        
        PlayerPrefs.SetInt("ShowFPS", value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetupResolution()
    {
        // Load saved resolution preferences (Default to Screen.width/height if no save exists)
        _resolution = new Vector2Int(PlayerPrefs.GetInt("ResWidth", Screen.width), PlayerPrefs.GetInt("ResHeight", Screen.height));

        // Screen.resolutions lists each size once per refresh rate; the menu only needs each size once.
        _resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!_resolutions.Contains(size)) _resolutions.Add(size);
        }
        if (!_resolutions.Contains(_resolution)) _resolutions.Add(_resolution);
        _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        List<string> options = new List<string>();
        foreach (Vector2Int size in _resolutions) options.Add(size.x + " x " + size.y);

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = _resolutions.IndexOf(_resolution);
        resolutionDropdown.RefreshShownValue();

        ApplyDisplay();

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    // Resolution and fullscreen always go together: switching only Screen.fullScreen drops back to
    // the desktop resolution, which is why a resolution picked in fullscreen didn't stick.
    private void ApplyDisplay()
    {
        FullScreenMode mode = _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(_resolution.x, _resolution.y, mode);
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
        _resolution = _resolutions[resolutionIndex];
        ApplyDisplay();

        // Save the player's choice!
        PlayerPrefs.SetInt("ResWidth", _resolution.x);
        PlayerPrefs.SetInt("ResHeight", _resolution.y);
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
        _fullscreen = isFullscreen;
        ApplyDisplay();

        // Save the player's choice! (1 for true, 0 for false)
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }
}
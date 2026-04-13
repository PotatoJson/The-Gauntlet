using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class AudioSettingsManager : MonoBehaviour
{
    [Header("Mixer Reference")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("Slider UI")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private TextMeshProUGUI masterText;

    [SerializeField] private Slider musicSlider;
    [SerializeField] private TextMeshProUGUI musicText;

    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI sfxText;

    [Header("Toggles")]
    [SerializeField] private Toggle muteInBackgroundToggle;

    private void Start()
    {
        // Set slider ranges to 0.0001 to 1 (Mixer math fails at 0)
        SetupSlider(masterSlider, "MasterVol", masterText);
        SetupSlider(musicSlider, "MusicVol", musicText);
        SetupSlider(sfxSlider, "SFXVol", sfxText);

        // Load "Mute in Background" preference
        bool mutePref = PlayerPrefs.GetInt("MuteBackground", 1) == 1;
        muteInBackgroundToggle.isOn = mutePref;
        muteInBackgroundToggle.onValueChanged.AddListener(SetMuteInBackground);
    }

    private void SetupSlider(Slider slider, string paramName, TextMeshProUGUI label)
    {
        // Load saved volume or default to 0.75
        float savedVol = PlayerPrefs.GetFloat(paramName, 0.75f);
        slider.value = savedVol;
        UpdateMixer(paramName, savedVol, label);

        // Add listener for when the player slides the bar
        slider.onValueChanged.AddListener((value) => {
            UpdateMixer(paramName, value, label);
            PlayerPrefs.SetFloat(paramName, value);
        });
    }

    private void UpdateMixer(string paramName, float value, TextMeshProUGUI label)
    {
        // Converts linear 0-1 slider value to logarithmic Decibels (-80 to 0)
        float dbValue = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
        mainMixer.SetFloat(paramName, dbValue);

        // Update the % text (e.g., "100%")
        if (label != null)
            label.text = Mathf.RoundToInt(value * 100) + "%";
    }

    public void SetMuteInBackground(bool mute)
    {
        PlayerPrefs.SetInt("MuteBackground", mute ? 1 : 0);
        // This is a Unity global setting
        Application.runInBackground = !mute;
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Owns the game's volume settings. It lives on one object that survives scene loads, so it cannot hold
/// references to a scene's sliders (the in-game settings menu is a different set of sliders in every scene,
/// and the old version only ever wired the main menu's: in the levels the volume sliders did nothing).
/// Instead it finds the audio rows of whichever settings menu is loaded and binds them every time a scene loads.
///
/// Volume goes to the Unity mixer (AudioSources) and, for Master, to the FMOD master bus (footsteps, attacks
/// and impacts are FMOD events and never went through the mixer).
/// </summary>
public class AudioSettingsManager : MonoBehaviour
{
    [Header("Mixer Reference")]
    [SerializeField] private AudioMixer mainMixer;

    public static AudioSettingsManager Instance { get; private set; }

    public const string MasterParam = "MasterVol";
    public const string MusicParam = "MusicVol";
    public const string SfxParam = "SFXVol";

    private const float DefaultVolume = 0.75f;

    // Row name in the settings menu -> mixer parameter.
    private static readonly Dictionary<string, string> RowToParam = new Dictionary<string, string>
    {
        { "Master Volume", MasterParam },
        { "Music Volume", MusicParam },
        { "SFX Volume", SfxParam },
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (Instance != this) return;

        ApplySavedVolumes();

        bool mutePref = PlayerPrefs.GetInt("MuteBackground", 1) == 1;
        Application.runInBackground = !mutePref;

        BindAudioRows();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this) return;

        ApplySavedVolumes(); // FMOD's bus is rebuilt with the scene's banks; the mixer keeps its values anyway
        BindAudioRows();
    }

    private readonly Dictionary<string, float> _mixerDb = new Dictionary<string, float>();
    private readonly HashSet<string> _pendingMixer = new HashSet<string>();
    private readonly List<string> _scratch = new List<string>();

    private void Update()
    {
        if (_pendingMixer.Count == 0 || mainMixer == null) return;

        _scratch.Clear();
        _scratch.AddRange(_pendingMixer);
        foreach (string param in _scratch)
        {
            if (mainMixer.SetFloat(param, _mixerDb[param])) _pendingMixer.Remove(param);
        }
    }

    #region Volume
    public static float GetSavedVolume(string paramName) => PlayerPrefs.GetFloat(paramName, DefaultVolume);

    public void ApplySavedVolumes()
    {
        ApplyVolume(MasterParam, GetSavedVolume(MasterParam));
        ApplyVolume(MusicParam, GetSavedVolume(MusicParam));
        ApplyVolume(SfxParam, GetSavedVolume(SfxParam));
    }

    /// <summary>Sends a 0-1 volume to the mixer and, for the master volume, to the FMOD master bus.</summary>
    public void ApplyVolume(string paramName, float value)
    {
        value = Mathf.Clamp01(value);

        if (mainMixer != null)
        {
            // Linear 0-1 to decibels (-80 to 0); the mixer maths breaks at exactly 0.
            _mixerDb[paramName] = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;

            // A mixer only comes alive once an AudioSource routes to it, and SetFloat fails until then. Keep
            // the value and keep trying (see Update) so the saved volume still lands on the first sound.
            if (!mainMixer.SetFloat(paramName, _mixerDb[paramName])) _pendingMixer.Add(paramName);
            else _pendingMixer.Remove(paramName);
        }

        if (paramName == MasterParam)
        {
            try
            {
                FMODUnity.RuntimeManager.GetBus("bus:/").setVolume(value);
            }
            catch (System.Exception)
            {
                // FMOD isn't ready (banks still loading): the next scene load applies it again.
            }
        }
    }

    public void SetVolume(string paramName, float value)
    {
        PlayerPrefs.SetFloat(paramName, value);
        ApplyVolume(paramName, value);
    }

    public void SetMuteInBackground(bool mute)
    {
        PlayerPrefs.SetInt("MuteBackground", mute ? 1 : 0);
        // This is a Unity global setting
        Application.runInBackground = !mute;
    }
    #endregion

    #region Binding
    // Finds the volume rows of every settings menu that is loaded (active or not) and hooks them up.
    private void BindAudioRows()
    {
        foreach (Slider slider in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform row = slider.transform.parent;
            if (row == null || !RowToParam.TryGetValue(row.name, out string param)) continue;
            if (slider.GetComponent<VolumeSliderBinding>() != null) continue;

            TextMeshProUGUI label = null;
            foreach (TextMeshProUGUI text in row.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.name.StartsWith("percentage")) { label = text; break; }
            }

            slider.gameObject.AddComponent<VolumeSliderBinding>().Init(this, param, label);
        }

        foreach (Toggle toggle in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (toggle.transform.parent == null || toggle.transform.parent.name != "BackGround Mute") continue;
            if (toggle.GetComponent<MuteToggleBinding>() != null) continue;

            toggle.gameObject.AddComponent<MuteToggleBinding>().Init(this);
        }
    }
    #endregion
}

/// <summary>One volume slider: shows the saved volume and applies changes. Added at runtime by the manager.</summary>
public class VolumeSliderBinding : MonoBehaviour
{
    private AudioSettingsManager _manager;
    private string _param;
    private TextMeshProUGUI _label;
    private Slider _slider;

    public void Init(AudioSettingsManager manager, string param, TextMeshProUGUI label)
    {
        _manager = manager;
        _param = param;
        _label = label;
        _slider = GetComponent<Slider>();

        // The percentage label was fed by a localisation entry, which would put its fixed text back whenever the
        // language changes. This script owns the text now.
        if (_label != null)
        {
            var localize = _label.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
            if (localize != null) localize.enabled = false;
        }

        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.SetValueWithoutNotify(AudioSettingsManager.GetSavedVolume(_param));
        UpdateLabel(_slider.value);

        _slider.onValueChanged.AddListener(OnChanged);
    }

    private void OnEnable()
    {
        // Re-read the saved value each time the menu opens, in case another menu changed it.
        if (_slider != null)
        {
            _slider.SetValueWithoutNotify(AudioSettingsManager.GetSavedVolume(_param));
            UpdateLabel(_slider.value);
        }
    }

    private void OnDestroy()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(float value)
    {
        _manager.SetVolume(_param, value);
        UpdateLabel(value);
    }

    private void UpdateLabel(float value)
    {
        if (_label != null) _label.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}

/// <summary>The "mute when the window loses focus" toggle, bound the same way.</summary>
public class MuteToggleBinding : MonoBehaviour
{
    private AudioSettingsManager _manager;
    private Toggle _toggle;

    public void Init(AudioSettingsManager manager)
    {
        _manager = manager;
        _toggle = GetComponent<Toggle>();
        _toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("MuteBackground", 1) == 1);
        _toggle.onValueChanged.AddListener(_manager.SetMuteInBackground);
    }

    private void OnDestroy()
    {
        if (_toggle != null && _manager != null) _toggle.onValueChanged.RemoveListener(_manager.SetMuteInBackground);
    }
}

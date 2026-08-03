using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    [Header("Checkpoint Resuming")]
    [Tooltip("Optional. Hook this button's OnClick to OnNewRunClicked. It is hidden automatically " +
             "when there is no checkpoint, because Play already starts a fresh run in that case.")]
    public Button newRunButton;

    [Tooltip("Optional label showing which level the saved run is in.")]
    public TMPro.TMP_Text saveSummaryText;

    [Header("Settings Connection")]
    public SettingsTabManager settingsTabManager;

    [Header("Focus Management")]
    [Tooltip("Drag your Main Menu 'Play' Button here so the controller knows where to return!")]
    public GameObject firstMainMenuButton;

    private bool _isUsingGamepad = false;

    private void Start()
    {
        // Gameplay leaves the cursor hidden and locked. Anything that reaches the menu without
        // going through the pause menu's ReturnToMainMenu - a level portal, for instance - used to
        // arrive with no cursor at all and nothing the player could click.
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Ensure the correct panels are active on startup
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);

        RefreshCheckpointUI();

        // Do a quick check on start
        if (Gamepad.current != null)
        {
            _isUsingGamepad = true;
            if (firstMainMenuButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(firstMainMenuButton);
            }
        }
        else if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null); // Keep it clean for mouse users!
        }
    }

    private void Update()
    {
        // 1. Switch TO Gamepad
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            if (!_isUsingGamepad)
            {
                _isUsingGamepad = true;

                // Only snap focus to the Play button if we are looking at the Main Menu
                if (mainMenuPanel.activeSelf && firstMainMenuButton != null && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(firstMainMenuButton);
                }
            }
        }
        // 2. Switch TO Keyboard/Mouse
        else if ((Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame) ||
                 (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f))
        {
            if (_isUsingGamepad)
            {
                _isUsingGamepad = false;

                // Instantly drop the highlight so it doesn't bother mouse users
                if (mainMenuPanel.activeSelf && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }
    }

    /// <summary>
    /// Picks up the last checkpoint if there is one, otherwise starts a fresh run.
    /// The player never has to choose: quitting and pressing Play just puts them back.
    /// </summary>
    public void OnPlayClicked()
    {
        Time.timeScale = 1f;

        if (SaveManager.Instance != null && SaveManager.Instance.HasSave)
        {
            // ResumeFromCheckpoint loads the saved scene itself and re-adopts the run ID,
            // so the telemetry run is continued rather than restarted.
            if (SaveManager.Instance.ResumeFromCheckpoint()) return;

            // The save turned out to be unreadable, so fall through to a fresh run.
            Debug.LogWarning("[MainMenu] The checkpoint could not be resumed. Starting a fresh run.");
        }

        StartFreshRun();
    }

    /// <summary>
    /// Throws away the checkpoint and starts over from the beginning.
    /// Hook this to the New Run button's OnClick.
    /// </summary>
    public void OnNewRunClicked()
    {
        StartFreshRun();
    }

    private void StartFreshRun()
    {
        if (MetricsTracker.Instance != null)
        {
            MetricsTracker.Instance.StartNewRun();
        }

        // A fresh run replaces any old one, so wipe the previous checkpoint.
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.StartNewRun();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("Dungeon Level");
    }

    /// <summary>
    /// Hides the New Run button when there is no checkpoint, since Play already starts
    /// a fresh run in that case and two buttons doing the same thing is just confusing.
    /// </summary>
    public void RefreshCheckpointUI()
    {
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave;

        if (newRunButton != null) newRunButton.gameObject.SetActive(hasSave);

        if (saveSummaryText != null)
        {
            GameSaveData save = hasSave ? SaveManager.Instance.PeekSave() : null;
            saveSummaryText.gameObject.SetActive(save != null);
            if (save != null) saveSummaryText.text = save.GetSummary();
        }
    }

    public void OnSettingsClicked()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);

        if (settingsTabManager != null)
        {
            settingsTabManager.InitializeSettingsMenu();
        }
    }

    public void OnTutorialClicked()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene("Tutorial");
    }
    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        // THE FIX: When returning to the Main Menu, ONLY grab focus back if they are using a controller!
        if (_isUsingGamepad && firstMainMenuButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstMainMenuButton);
        }
        else if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnQuitClicked()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}
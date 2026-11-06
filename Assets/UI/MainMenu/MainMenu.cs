using DG.Tweening;
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
    [Tooltip("New Game: hook its OnClick to OnNewRunClicked. Always shown; it plays the prologue.")]
    public Button newRunButton;

    [Tooltip("Continue: hook its OnClick to OnPlayClicked. Greyed out when there is no checkpoint to resume.")]
    public Button continueButton;

    [Tooltip("Optional label showing which level the saved run is in.")]
    public TMPro.TMP_Text saveSummaryText;

    [Header("Settings Connection")]
    public SettingsTabManager settingsTabManager;

    [Header("Focus Management")]
    [Tooltip("Drag your Main Menu 'Play' Button here so the controller knows where to return!")]
    public GameObject firstMainMenuButton;

    [Header("Transition")]
    [Tooltip("Full-screen black overlay (CanvasGroup) that fades in when Play / New Run is pressed.")]
    public CanvasGroup fadeOverlay;
    public float fadeDuration = 0.9f;

    private bool _isUsingGamepad = false;
    private bool _isLeaving = false;

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
        FadeThen(PlayOrContinue);
    }

    private void PlayOrContinue()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave)
        {
            // ResumeFromCheckpoint loads the saved scene itself and re-adopts the run ID,
            // so the telemetry run is continued rather than restarted.
            if (SaveManager.Instance.ResumeFromCheckpoint()) return;

            // The save turned out to be unreadable, so fall through to a fresh run.
            Debug.LogWarning("[MainMenu] The checkpoint could not be resumed. Starting a fresh run.");
        }

        StartFreshRun(false); // only New Game tells the prologue
    }

    /// <summary>
    /// Throws away the checkpoint and starts over from the beginning.
    /// Hook this to the New Run button's OnClick.
    /// </summary>
    public void OnNewRunClicked()
    {
        FadeThen(() => StartFreshRun(true));
    }

    // Fades the screen to black, then runs the action that loads the next scene.
    private void FadeThen(System.Action load)
    {
        if (_isLeaving) return;
        _isLeaving = true;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        if (fadeOverlay == null) { load(); return; }

        fadeOverlay.transform.SetAsLastSibling();
        fadeOverlay.blocksRaycasts = true; // no more clicks while leaving
        fadeOverlay.DOFade(1f, fadeDuration).SetEase(Ease.InOutSine).SetUpdate(true).OnComplete(() => load());
    }

    private void StartFreshRun(bool withPrologue)
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

        // The Backpack outlives the trip to the menu: without this, the last session's gauntlets and gems
        // (or whatever the tutorial left behind) would come along into the new run.
        if (PersistentEquipment.Instance != null)
        {
            PersistentEquipment.Instance.ResetForNewRun();
        }

        Time.timeScale = 1f;
        StoryDialogue.NewRun();

        if (!withPrologue)
        {
            SceneManager.LoadScene("Dungeon Level");
            return;
        }

        // The prologue, over the faded-out menu: the night Hell took the knight's family and his sword shattered.
        // It ends by falling into the pit, straight into the Dungeon.
        StoryDialogue.Tell(() => SceneManager.LoadScene("Dungeon Level"), "Story_Pro_03", new Vector2(0.5f, 0.33f),
            ("Story_Pro_01", Speaker.Narrator),
            ("Story_Pro_02", Speaker.Narrator),
            ("Story_Pro_03", Speaker.Narrator),
            ("Story_Pro_04", Speaker.Narrator),
            ("Story_Pro_05", Speaker.Narrator),
            ("Story_Pro_06", Speaker.Narrator),
            ("Story_Pro_07", Speaker.Narrator));
    }

    /// <summary>
    /// Greys out Continue when there is no checkpoint: there is nothing to continue, and New Game is the way in.
    /// Greyed rather than hidden, so the buttons don't leave a gap. A gamepad skips it.
    /// </summary>
    public void RefreshCheckpointUI()
    {
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave;

        if (continueButton != null)
        {
            continueButton.interactable = hasSave;

            // The menu's buttons show their frame only on hover; disabled would show it half-faded.
            ColorBlock colors = continueButton.colors;
            colors.disabledColor = colors.normalColor;
            continueButton.colors = colors;

            CanvasGroup look = continueButton.GetComponent<CanvasGroup>();
            if (look == null) look = continueButton.gameObject.AddComponent<CanvasGroup>();
            look.alpha = hasSave ? 1f : 0.35f;
        }

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
        FadeThen(() => SceneManager.LoadScene("Tutorial"));
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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    [Header("Settings Connection")]
    public SettingsTabManager settingsTabManager;

    [Header("Focus Management")]
    [Tooltip("Drag your Main Menu 'Play' Button here so the controller knows where to return!")]
    public GameObject firstMainMenuButton;

    private bool _isUsingGamepad = false;

    private void Start()
    {
        // Ensure the correct panels are active on startup
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);

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

    public void OnPlayClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Dungeon Level");
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
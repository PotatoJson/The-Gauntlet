using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GauntletMenu : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private RectTransform gauntletImage;
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private Button firstSelectedButton;
    [SerializeField] private Button restartButton; // Separate slot for the finger button
    [SerializeField] private SettingsTabManager settingsTabManager;

    [Header("Buttons for Navigation")]
    [Tooltip("Order: Resume, Setting, Main Menu, Quit")]
    [SerializeField] private List<Button> menuButtons;

    [Header("Input References")]
    [SerializeField] private InputActionAsset inputActions;
    private InputActionMap _playerMap;
    private InputActionMap _uiMap;
    private InputAction _pauseAction;

    [Header("Intro Animation")]
    [SerializeField] private float slideDuration = 0.6f;
    [SerializeField] private Ease slideEase = Ease.OutBack;

    [Header("Button Breathing")]
    [SerializeField] private float breathScale = 1.05f;
    [SerializeField] private float breathSpeed = 1.5f;

    [Header("Settings Transition")]
    [SerializeField] private RectTransform settingsPanel;
    [SerializeField] private CanvasGroup mainButtonsGroup;
    [SerializeField] private float transitionSpeed = 0.5f;

    [Header("Character Menu References")]
    [SerializeField] private GameObject characterScreenRoot;
    [SerializeField] private GameObject pauseMenuUpgradeButton;

    private bool _isPaused = false;
    private Vector2 _centerPosition = Vector2.zero;
    private float _offscreenPosX;
    private Vector3 _originalGauntletScale;
    private Dictionary<RectTransform, Vector3> _originalButtonScales = new Dictionary<RectTransform, Vector3>();
    private Vector2 _leftPosition = new Vector2(-867, 0);
    private Vector3 _settingsRotation = new Vector3(0, 0, 90);

    private void Awake()
    {
        _playerMap = inputActions.FindActionMap("Player");
        _uiMap = inputActions.FindActionMap("UI");
        _pauseAction = _playerMap.FindAction("Pause");

        _offscreenPosX = -Screen.width;
        _originalGauntletScale = gauntletImage.localScale;

        // Collect all buttons including Restart for the animation
        List<Button> allButtons = new List<Button>(menuButtons);

        foreach (var btn in allButtons)
        {
            RectTransform rect = btn.GetComponentInChildren<Text>()?.rectTransform ?? btn.GetComponent<RectTransform>();
            if (rect != null) _originalButtonScales[rect] = rect.localScale;
        }

        menuCanvas.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        gauntletImage.anchoredPosition = new Vector2(_offscreenPosX, 0);
    }

    private void OnEnable()
    {
        _pauseAction.Enable();
        _pauseAction.performed += OnPausePerformed;
    }

    private void OnDisable()
    {
        _pauseAction.Disable();
        _pauseAction.performed -= OnPausePerformed;
    }

    private void Update()
    {
        if (!_isPaused) return;

        // REMOVE the settingsPanel check so mouse detection works in all menus

        bool mouseMoved = Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f;
        bool keyboardPressed = Keyboard.current != null &&
                           Keyboard.current.anyKey.wasPressedThisFrame &&
                           !Keyboard.current.escapeKey.wasPressedThisFrame;

        if (mouseMoved || keyboardPressed)
        {
            ShowCursor();

            if (EventSystem.current.currentSelectedGameObject != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        if (Gamepad.current != null)
        {
            // NEW: We added the upgradePanel check here so the Pause Menu ignores the East Button if you are upgrading!
            if (Gamepad.current.buttonEast.wasPressedThisFrame &&
                !settingsPanel.gameObject.activeSelf &&
                !characterScreenRoot.activeSelf)
            {
                ResumeGame();
                return;
            }

            bool stickMoved = Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.5f;
            bool dpadPressed = Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.5f;

            if (stickMoved || dpadPressed)
            {
                HideCursor();

                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    // Context-aware selection
                    if (settingsPanel.gameObject.activeSelf)
                        settingsTabManager.FocusCurrentTab();
                    else if (characterScreenRoot.activeSelf)
                    {
                        // NEW: Do nothing! We are in the upgrade menu, so don't steal focus.
                    }
                    else if (firstSelectedButton != null)
                        EventSystem.current.SetSelectedGameObject(firstSelectedButton.gameObject);
                }
            }
        }
    }

    private void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None; // Allows mouse to move freely
    }

    private void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked; // Centers and hides the cursor to avoid accidental hovers
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (characterScreenRoot != null && characterScreenRoot.activeSelf)
        {
            return;
        }

        if (!_isPaused)
        {
            PauseGame();
        }
        else
        {
            // Back button logic: Close settings if open
            if (settingsPanel.gameObject.activeSelf)
            {
                CloseSettings();
            }
            // This allows your InventoryManager.cs to safely handle the Escape key instead.
            else if (characterScreenRoot != null && characterScreenRoot.activeSelf) // UPDATE HERE
            {
                return;
            }
            // If no menus are open, resume the game.
            else
            {
                ResumeGame();
            }
        }
    }

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        menuCanvas.SetActive(true);

        _playerMap.Disable();
        _uiMap.Enable();
        _pauseAction.Enable();

        if (Gamepad.current != null && firstSelectedButton != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton.gameObject);
        }

        gauntletImage.DOKill();
        StopAllBreathingAnimations(); // Clean up previous animations

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        gauntletImage.anchoredPosition = new Vector2(_offscreenPosX, 0);
        gauntletImage.DOAnchorPos(_centerPosition, slideDuration)
            .SetEase(slideEase)
            .SetUpdate(true)
            .OnComplete(StartBreathing);
    }

    public void ResumeGame()
    {
        _isPaused = false;
        _uiMap.Disable();
        _playerMap.Enable();

        // Lock cursor after pressing Esc
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Sequence exitSequence = DOTween.Sequence();
        exitSequence.Join(gauntletImage.DOAnchorPos(new Vector2(_offscreenPosX, 0), slideDuration * 0.8f).SetEase(Ease.InBack));
        exitSequence.Join(gauntletImage.DORotate(Vector3.zero, slideDuration * 0.8f).SetEase(Ease.InBack));

        exitSequence.SetUpdate(true);
        exitSequence.OnComplete(() => {
            menuCanvas.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            Time.timeScale = 1f;
        });
    }

    private void StartBreathing()
    {
        foreach (var rect in _originalButtonScales.Keys)
        {
            if (rect == null) continue;
            Vector3 targetScale = _originalButtonScales[rect] * breathScale;

            rect.DOScale(targetScale, breathSpeed)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    private void StopAllBreathingAnimations()
    {
        foreach (var rect in _originalButtonScales.Keys)
        {
            if (rect != null)
            {
                rect.DOKill();
                rect.localScale = _originalButtonScales[rect];
            }
        }
    }

    public void OpenSettings()
    {
        EventSystem.current.SetSelectedGameObject(null);

        mainButtonsGroup.DOFade(0, 0.2f).SetUpdate(true);
        mainButtonsGroup.interactable = false;
        mainButtonsGroup.blocksRaycasts = false;

        Sequence settingsSequence = DOTween.Sequence();
        settingsSequence.Join(gauntletImage.DOAnchorPos(_leftPosition, transitionSpeed).SetEase(Ease.InOutQuad));
        settingsSequence.Join(gauntletImage.DORotate(_settingsRotation, transitionSpeed).SetEase(Ease.InOutQuad));

        settingsSequence.OnComplete(() => {
            ShowSettingsPanel();
            if (Gamepad.current != null && settingsTabManager != null)
            {
                settingsTabManager.InitializeSettingsMenu();
            }
        });
        settingsSequence.SetUpdate(true);
    }

    private void ShowSettingsPanel()
    {
        settingsPanel.gameObject.SetActive(true);
        float panelWidth = settingsPanel.rect.width;
        settingsPanel.anchoredPosition = new Vector2(-panelWidth, 0);

        settingsPanel.DOAnchorPos(new Vector2(350, 0), 0.4f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    public void CloseSettings()
    {
        settingsPanel.DOAnchorPos(new Vector2(-Screen.width, 0), 0.3f)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .OnComplete(() => {
                settingsPanel.gameObject.SetActive(false);

                Sequence returnSequence = DOTween.Sequence();
                returnSequence.Join(gauntletImage.DOAnchorPos(Vector2.zero, transitionSpeed).SetEase(Ease.InOutQuad));
                returnSequence.Join(gauntletImage.DORotate(Vector3.zero, transitionSpeed).SetEase(Ease.InOutQuad));

                returnSequence.OnComplete(() => {
                    mainButtonsGroup.DOFade(1, 0.2f).SetUpdate(true);
                    mainButtonsGroup.interactable = true;
                    mainButtonsGroup.blocksRaycasts = true;
                    StartBreathing();

                    if (Gamepad.current != null && firstSelectedButton != null)
                    {
                        EventSystem.current.SetSelectedGameObject(firstSelectedButton.gameObject);
                    }
                });

                returnSequence.SetUpdate(true);
            });
    }


    // Call this from the Upgrade Menu's "Return" Button OnClick() AND the Unity Event
    // Call this from the Pause Menu's "Upgrade/Character" Button OnClick()
    public void OpenUpgradeMenu()
    {
        characterScreenRoot.SetActive(true);
        mainButtonsGroup.interactable = false;
        mainButtonsGroup.blocksRaycasts = false;
    }

    // Call this from the Character Screen's "Return" Button AND your InventoryManager UnityEvent
    public void CloseUpgradeMenu()
    {
        characterScreenRoot.SetActive(false);

        mainButtonsGroup.interactable = true;
        mainButtonsGroup.blocksRaycasts = true;

        // Pass controller focus safely back to the Pause Menu
        if (EventSystem.current != null && pauseMenuUpgradeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(pauseMenuUpgradeButton);
        }
    }
    public void RestartGame() { CleanupTweens();  Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void ReturnToMainMenu() { CleanupTweens();  Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); ShowCursor(); }
    public void QuitGame() { Application.Quit(); }

    private void OnDestroy() { CleanupTweens(); }
    private void CleanupTweens()
    {
        // Kill the infinite breathing loops
        StopAllBreathingAnimations();

        // Kill any movement or rotation on the gauntlet image
        if (gauntletImage != null)
        {
            gauntletImage.DOKill();
        }

        // Kill any panel transitions
        if (settingsPanel != null)
        {
            settingsPanel.DOKill();
        }
    }

}
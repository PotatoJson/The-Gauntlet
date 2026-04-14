using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // Added for scene loading

public class GauntletMenu : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private RectTransform gauntletImage;
    [SerializeField] private GameObject menuCanvas;

    [Header("Intro Animation")]
    [SerializeField] private float slideDuration = 0.6f;
    [SerializeField] private Ease slideEase = Ease.OutBack;

    [Header("Button Breathing")]
    [SerializeField] private List<RectTransform> buttonTextRects;
    [SerializeField] private float breathScale = 1.05f;
    [SerializeField] private float breathSpeed = 1.5f;

    [Header("Settings Transition")]
    [SerializeField] private RectTransform settingsPanel;
    [SerializeField] private CanvasGroup mainButtonsGroup;
    [SerializeField] private float transitionSpeed = 0.5f;

    private bool _isPaused = false;
    private Vector2 _centerPosition = Vector2.zero;
    private float _offscreenPosX;

    private Vector3 _originalGauntletScale;
    private Dictionary<RectTransform, Vector3> _originalButtonScales = new Dictionary<RectTransform, Vector3>();

    private Vector2 _leftPosition = new Vector2(-867, 0);
    private Vector3 _settingsRotation = new Vector3(0, 0, 90);

    private void Awake()
    {
        _offscreenPosX = -Screen.width;
        _originalGauntletScale = gauntletImage.localScale;

        foreach (var rect in buttonTextRects)
        {
            if (rect != null) _originalButtonScales[rect] = rect.localScale;
        }

        menuCanvas.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        gauntletImage.anchoredPosition = new Vector2(_offscreenPosX, 0);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    // --- NEW: MENU FUNCTIONALITY ---

    public void RestartGame()
    {
        // Must reset time scale or the game will be frozen when the scene reloads
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        // Make sure your main menu scene is named exactly "MainMenu" in Build Settings
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // --- EXISTING ANIMATION LOGIC ---

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        menuCanvas.SetActive(true);

        gauntletImage.DOKill();
        foreach (var rect in buttonTextRects) rect.DOKill();

        gauntletImage.localScale = _originalGauntletScale;
        gauntletImage.localEulerAngles = Vector3.zero;

        foreach (var rect in buttonTextRects)
        {
            if (rect != null) rect.localScale = _originalButtonScales[rect];
        }

        mainButtonsGroup.alpha = 1;
        mainButtonsGroup.interactable = true;
        mainButtonsGroup.blocksRaycasts = true;
        settingsPanel.gameObject.SetActive(false);

        gauntletImage.anchoredPosition = new Vector2(_offscreenPosX, 0);
        gauntletImage.DOAnchorPos(_centerPosition, slideDuration)
            .SetEase(slideEase)
            .SetUpdate(true)
            .OnComplete(StartBreathing);
    }

    public void ResumeGame()
    {
        _isPaused = false;

        foreach (var rect in buttonTextRects) rect.DOKill();
        gauntletImage.DOKill();

        Sequence exitSequence = DOTween.Sequence();
        exitSequence.Join(gauntletImage.DOAnchorPos(new Vector2(_offscreenPosX, 0), slideDuration * 0.8f).SetEase(Ease.InBack));
        exitSequence.Join(gauntletImage.DORotate(Vector3.zero, slideDuration * 0.8f).SetEase(Ease.InBack));
        exitSequence.Join(gauntletImage.DOScale(_originalGauntletScale, 0.2f));

        exitSequence.SetUpdate(true);
        exitSequence.OnComplete(() => {
            menuCanvas.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            Time.timeScale = 1f;
        });
    }

    private void StartBreathing()
    {
        foreach (var rect in buttonTextRects)
        {
            if (rect == null) continue;
            Vector3 targetScale = _originalButtonScales[rect] * breathScale;

            rect.DOScale(targetScale, breathSpeed)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    public void OpenSettings()
    {
        mainButtonsGroup.DOFade(0, 0.2f).SetUpdate(true);
        mainButtonsGroup.interactable = false;
        mainButtonsGroup.blocksRaycasts = false;

        Sequence settingsSequence = DOTween.Sequence();
        settingsSequence.Join(gauntletImage.DOAnchorPos(_leftPosition, transitionSpeed).SetEase(Ease.InOutQuad));
        settingsSequence.Join(gauntletImage.DORotate(_settingsRotation, transitionSpeed).SetEase(Ease.InOutQuad));

        settingsSequence.OnComplete(() => {
            ShowSettingsPanel();
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
                });

                returnSequence.SetUpdate(true);
            });
    }
}
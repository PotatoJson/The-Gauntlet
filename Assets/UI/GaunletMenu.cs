using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GauntletMenu : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private RectTransform gauntletImage;
    [SerializeField] private GameObject menuCanvas; // The "PauseMenu" parent object

    [Header("Intro Animation")]
    [SerializeField] private float slideDuration = 0.6f;
    [SerializeField] private Ease slideEase = Ease.OutBack;

    [Header("Button Breathing")]
    [SerializeField] private List<RectTransform> buttonTextRects;
    [SerializeField] private float breathScale = 1.05f;
    [SerializeField] private float breathSpeed = 1.5f;

    private bool _isPaused = false;
    private Vector2 _centerPosition = Vector2.zero;
    private float _offscreenPosX;

    private void Awake()
    {
        // Calculate off-screen position based on screen width
        _offscreenPosX = -Screen.width;

        // Ensure menu is hidden at start
        menuCanvas.SetActive(false);
        gauntletImage.anchoredPosition = new Vector2(_offscreenPosX, 0);
    }

    private void Update()
    {
        // Listen for the Escape key
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f; // Freeze the game
        menuCanvas.SetActive(true);

        // Kill any previous tweens to prevent "shaking"
        gauntletImage.DOKill();

        // Start off-screen and slide to (0,0) center
        gauntletImage.anchoredPosition = new Vector2(_offscreenPosX, 0);
        gauntletImage.DOAnchorPos(_centerPosition, slideDuration)
            .SetEase(slideEase)
            .SetUpdate(true) // Crucial: lets DOTween run while Time.timeScale = 0
            .OnComplete(StartBreathing);
    }

    public void ResumeGame()
    {
        _isPaused = false;

        // Slide out to the left
        gauntletImage.DOAnchorPos(new Vector2(_offscreenPosX, 0), slideDuration * 0.8f)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() => {
                menuCanvas.SetActive(false);
                Time.timeScale = 1f; // Unfreeze the game
            });

        // Stop breathing effects
        foreach (var rect in buttonTextRects) rect.DOKill();
    }

    private void StartBreathing()
    {
        foreach (var rect in buttonTextRects)
        {
            rect.DOScale(breathScale, breathSpeed)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }
}
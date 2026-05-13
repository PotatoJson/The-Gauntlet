using UnityEngine;
using UnityEngine.InputSystem;

public class AltCursorReveal : MonoBehaviour
{
    [Header("Player References")]
    [Tooltip("Drag the Player (or whoever holds PlayerManager) here!")]
    [SerializeField] private PlayerManager playerManager;

    [Header("Input References")]
    [Tooltip("Drag your Player's 'Look' input action here so we can freeze the camera!")]
    [SerializeField] private InputActionReference lookAction;

    [Header("UI References")]
    [Tooltip("Drag the CanvasGroup of the HUD/Menu you want to toggle here!")]
    [SerializeField] private CanvasGroup altMenuCanvasGroup;

    private bool _isMenuOpen = false;

    private void Start()
    {
        // Ensure the menu starts hidden and unclickable when the game loads!
        if (altMenuCanvasGroup != null)
        {
            altMenuCanvasGroup.alpha = 0f;
            altMenuCanvasGroup.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Time.timeScale == 0f) return;

        // --- ATTEMPT TO SHOW CURSOR & HUD ---
        if (Keyboard.current.altKey.wasPressedThisFrame)
        {
            if (playerManager != null && playerManager.IsInCombat)
            {
                Debug.Log("Alt Menu blocked: Player is in combat!");
                return;
            }

            _isMenuOpen = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (lookAction != null) lookAction.action.Disable();

            // Turn ON the HUD via CanvasGroup
            if (altMenuCanvasGroup != null)
            {
                altMenuCanvasGroup.alpha = 1f;
                altMenuCanvasGroup.blocksRaycasts = true;
            }
        }

        // --- ATTEMPT TO HIDE CURSOR & HUD ---
        else if (Keyboard.current.altKey.wasReleasedThisFrame)
        {
            if (!_isMenuOpen) return;

            _isMenuOpen = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            if (lookAction != null) lookAction.action.Enable();

            // Turn OFF the HUD via CanvasGroup
            if (altMenuCanvasGroup != null)
            {
                altMenuCanvasGroup.alpha = 0f;
                altMenuCanvasGroup.blocksRaycasts = false;
            }
        }
    }
}
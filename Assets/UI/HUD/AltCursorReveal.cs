using UnityEngine;
using UnityEngine.InputSystem;

public class AltCursorReveal : MonoBehaviour
{
    [Header("Player References")]
    [Tooltip("Drag the Player (or whoever holds PlayerManager) here! Left empty, the player is " +
             "found automatically, which is what happens in every level except the Tutorial.")]
    [SerializeField] private PlayerManager playerManager;

    [Header("Input References")]
    [Tooltip("Drag your Player's 'Look' input action here so we can freeze the camera!")]
    [SerializeField] private InputActionReference lookAction;

    [Header("UI References")]
    [Tooltip("Drag the CanvasGroup of the HUD/Menu you want to toggle here!")]
    [SerializeField] private CanvasGroup altMenuCanvasGroup;

    [Tooltip("Drag your CombatHUDFader here. Alt then reveals EVERY element it manages (including " +
             "the potions) instead of just the CanvasGroup above, and the two stop fighting over " +
             "the same alpha. If this is left empty the old direct-alpha behaviour is used.")]
    [SerializeField] private CombatHUDFader combatHUDFader;

    private bool _isMenuOpen = false;

    private void Start()
    {
        // The fader lives on the Canvas prefab while this script lives on UIManager, so the
        // reference cannot be baked into either prefab. Find it in the scene instead.
        if (combatHUDFader == null)
        {
            combatHUDFader = FindFirstObjectByType<CombatHUDFader>(FindObjectsInactive.Include);
        }

        // Ensure the menu starts hidden and unclickable when the game loads!
        if (altMenuCanvasGroup != null)
        {
            // When the fader is wired up it owns the alpha, so only the raycast state is ours.
            if (combatHUDFader == null) altMenuCanvasGroup.alpha = 0f;
            altMenuCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// True while the player is fighting. The reveal is locked out then.
    ///
    /// The PlayerManager reference is resolved lazily because it is left unassigned on the Canvas
    /// prefab, and the player is spawned per level rather than living alongside this UI. Without
    /// this the combat check silently passed and Alt worked mid-fight.
    /// </summary>
    private bool IsPlayerInCombat()
    {
        if (playerManager == null)
        {
            playerManager = FindFirstObjectByType<PlayerManager>();
            if (playerManager == null) return false; // No player yet, e.g. a menu scene.
        }

        return playerManager.IsInCombat;
    }

    /// <summary>
    /// Shows or hides the HUD. Routed through CombatHUDFader when one is assigned so a single
    /// component owns the alpha; otherwise falls back to setting it directly.
    /// </summary>
    private void SetHudVisible(bool visible)
    {
        if (combatHUDFader != null)
        {
            combatHUDFader.SetForcedVisible(visible);
        }
        else if (altMenuCanvasGroup != null)
        {
            altMenuCanvasGroup.alpha = visible ? 1f : 0f;
        }

        if (altMenuCanvasGroup != null) altMenuCanvasGroup.blocksRaycasts = visible;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Time.timeScale == 0f) return;

        // Combat can start while Alt is already held. Slam it shut rather than leaving the player
        // fighting with a free cursor and a frozen camera.
        if (_isMenuOpen && IsPlayerInCombat())
        {
            Debug.Log("Alt Menu closed: combat started.");
            CloseReveal();
            return;
        }

        // --- ATTEMPT TO SHOW CURSOR & HUD ---
        if (Keyboard.current.altKey.wasPressedThisFrame)
        {
            if (IsPlayerInCombat())
            {
                Debug.Log("Alt Menu blocked: Player is in combat!");
                return;
            }

            OpenReveal();
        }

        // --- ATTEMPT TO HIDE CURSOR & HUD ---
        else if (Keyboard.current.altKey.wasReleasedThisFrame)
        {
            if (!_isMenuOpen) return;

            CloseReveal();
        }
    }

    private void OpenReveal()
    {
        _isMenuOpen = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (lookAction != null) lookAction.action.Disable();

        SetHudVisible(true);
    }

    private void CloseReveal()
    {
        _isMenuOpen = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (lookAction != null) lookAction.action.Enable();

        SetHudVisible(false);
    }
}
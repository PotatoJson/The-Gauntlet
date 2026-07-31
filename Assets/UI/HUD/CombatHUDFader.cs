using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class CombatHUDFader : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your Player object here to read the combat state")]
    public PlayerManager playerManager;

    [Tooltip("Drag your Player object here to read the health state")]
    public PlayerHealth playerHealth; // --- NEW REFERENCE ---

    [Tooltip("Drag the UI elements you want to fade here. They MUST have a CanvasGroup component!")]
    public List<CanvasGroup> elementsToFade;

    [Header("Settings")]
    [Tooltip("How many seconds it takes to fade in or out")]
    public float fadeDuration = 0.5f;

    private bool _wasVisible;
    private bool _forcedVisible;

    private void Start()
    {
        if (playerManager == null) return;

        // Set the initial state instantly when the game starts
        _wasVisible = ShouldBeVisible();
        ForceAlpha(_wasVisible ? 1f : 0f);
    }

    private void Update()
    {
        // Unchanged from before: with no PlayerManager assigned this component does not drive
        // the HUD at all. SetForcedVisible still works, so the Alt reveal is unaffected.
        if (playerManager == null) return;

        Evaluate();
    }

    /// <summary>
    /// Holds the whole HUD open regardless of combat state, for the Alt reveal.
    ///
    /// Callers must go through this rather than writing CanvasGroup.alpha themselves. Two scripts
    /// setting the same alpha independently used to desync this component: it only fades on a
    /// change, so an outside write left _wasVisible disagreeing with what was on screen and the
    /// HUD could stay invisible for an entire fight.
    /// </summary>
    public void SetForcedVisible(bool forced)
    {
        if (_forcedVisible == forced) return;

        _forcedVisible = forced;
        Evaluate(); // Respond now instead of waiting for the next frame.
    }

    private void Evaluate()
    {
        bool shouldBeVisible = ShouldBeVisible();
        if (shouldBeVisible == _wasVisible) return;

        _wasVisible = shouldBeVisible;
        FadeAllTo(_wasVisible ? 1f : 0f);
    }

    private bool ShouldBeVisible()
    {
        // The Alt reveal wins over everything else.
        if (_forcedVisible) return true;

        bool inCombat = playerManager != null && playerManager.IsInCombat;

        bool hasLevelUp = ProgressBarCircle.Instance != null &&
                          ProgressBarCircle.Instance.HasPendingLevelUps;

        // Keep the UI visible if health is not full
        bool isMissingHealth = playerHealth != null &&
                               playerHealth.CurrentHealth < playerHealth.MaxHealth;

        return inCombat || hasLevelUp || isMissingHealth;
    }

    private void FadeAllTo(float targetAlpha)
    {
        foreach (CanvasGroup cg in elementsToFade)
        {
            if (cg != null)
            {
                // Kill any active fades so they don't glitch, then start the new fade
                cg.DOKill();
                cg.DOFade(targetAlpha, fadeDuration).SetUpdate(true);
            }
        }
    }

    private void ForceAlpha(float alpha)
    {
        foreach (CanvasGroup cg in elementsToFade)
        {
            if (cg != null) cg.alpha = alpha;
        }
    }
}
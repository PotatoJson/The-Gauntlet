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

    private void Start()
    {
        if (playerManager != null)
        {
            // Set the initial state instantly when the game starts
            bool hasLevelUp = (ProgressBarCircle.Instance != null && ProgressBarCircle.Instance.HasPendingLevelUps);

            // --- NEW: Check if missing health on start ---
            bool isMissingHealth = (playerHealth != null && playerHealth.CurrentHealth < playerHealth.MaxHealth);

            _wasVisible = playerManager.IsInCombat || hasLevelUp || isMissingHealth;

            ForceAlpha(_wasVisible ? 1f : 0f);
        }
    }

    private void Update()
    {
        if (playerManager == null) return;

        bool inCombat = playerManager.IsInCombat;

        bool hasLevelUp = false;
        if (ProgressBarCircle.Instance != null)
        {
            hasLevelUp = ProgressBarCircle.Instance.HasPendingLevelUps;
        }

        // --- NEW: Keep UI visible if health is not full ---
        bool isMissingHealth = false;
        if (playerHealth != null)
        {
            isMissingHealth = playerHealth.CurrentHealth < playerHealth.MaxHealth;
        }

        // The UI should be visible if ANY of these three things are true!
        bool shouldBeVisible = (inCombat || hasLevelUp || isMissingHealth);

        // If visibility changed this frame then fade
        if (shouldBeVisible != _wasVisible)
        {
            _wasVisible = shouldBeVisible;

            if (_wasVisible)
            {
                FadeAllTo(1f); // Fade IN
            }
            else
            {
                FadeAllTo(0f); // Fade OUT
            }
        }
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
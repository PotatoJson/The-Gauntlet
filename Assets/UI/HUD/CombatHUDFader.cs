using UnityEngine;
using System.Collections.Generic;
using DG.Tweening; // We use DOTween for the smooth fading!

public class CombatHUDFader : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your Player object here to read the combat state")]
    public PlayerManager playerManager;

    [Tooltip("Drag the UI elements you want to fade here. They MUST have a CanvasGroup component!")]
    public List<CanvasGroup> elementsToFade;

    [Header("Settings")]
    [Tooltip("How many seconds it takes to fade in or out")]
    public float fadeDuration = 0.5f;

    private bool _wasInCombat;

    private void Start()
    {
        if (playerManager != null)
        {
            // Set the initial state instantly when the game starts
            _wasInCombat = playerManager.IsInCombat;
            ForceAlpha(_wasInCombat ? 1f : 0f);
        }
    }

    private void Update()
    {
        if (playerManager == null) return;

        // If the player's combat state just changed this frame...
        if (playerManager.IsInCombat != _wasInCombat)
        {
            _wasInCombat = playerManager.IsInCombat;

            if (_wasInCombat)
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
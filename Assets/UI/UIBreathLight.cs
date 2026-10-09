using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIBreathingLight : MonoBehaviour
{
    [Header("Settings")]
    public float breathDuration = 1.5f;
    public Ease breathEase = Ease.InOutSine;

    [Header("UI Reference")]
    [Tooltip("Drag your new Glowing_Eye UI Image here")]
    public Image glowingEyeImage;

    [Range(0f, 1f)] public float minAlpha = 0.1f;
    [Range(0f, 1f)] public float maxAlpha = 1.0f;

    [Tooltip("Extra size swell at full brightness, e.g. 0.12 = +12%. 0 = alpha only.")]
    public float scalePulse = 0f;
    [Tooltip("Seconds to wait before starting, so two eyes don't breathe in perfect lockstep.")]
    public float startDelay = 0f;

    private void Start()
    {
        if (glowingEyeImage != null)
        {
            // Set the starting opacity
            Color startColor = glowingEyeImage.color;
            startColor.a = minAlpha;
            glowingEyeImage.color = startColor;

            // Animate the alpha (opacity) up and down infinitely!
            glowingEyeImage.DOFade(maxAlpha, breathDuration)
                .SetEase(breathEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(startDelay)
                .SetUpdate(true); // SetUpdate(true) ensures it plays even if the menu is paused

            if (scalePulse > 0f)
            {
                Vector3 baseScale = glowingEyeImage.rectTransform.localScale;
                glowingEyeImage.rectTransform.DOScale(baseScale * (1f + scalePulse), breathDuration)
                    .SetEase(breathEase)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(startDelay)
                    .SetUpdate(true);
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up the tween if the menu closes
        if (glowingEyeImage != null)
        {
            glowingEyeImage.DOKill();
            glowingEyeImage.rectTransform.DOKill();
        }
    }
}
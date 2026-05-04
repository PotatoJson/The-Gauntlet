using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // Required for DOTween

// This automatically adds a CanvasGroup to your object if you forget!
[RequireComponent(typeof(CanvasGroup))]
public class BossHealthBar : MonoBehaviour
{
    public static BossHealthBar Instance;

    [Header("UI References")]
    [SerializeField] private GameObject healthBarContainer;
    [SerializeField] private Image fillImage;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float introFillDuration = 1.0f; // How fast it shoots left-to-right
    [SerializeField] private float damageDropDuration = 0.25f; // Smooth health drain when hit

    private CanvasGroup _canvasGroup;
    private Tween _healthTween;

    private void Awake()
    {
        Instance = this;
        _canvasGroup = GetComponent<CanvasGroup>();

        if (healthBarContainer != null)
        {
            healthBarContainer.SetActive(false);
        }
    }

    public void ShowBossHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBarContainer == null) return;

        // 1. Instantly turn it on, but keep it completely invisible and empty
        healthBarContainer.SetActive(true);
        _canvasGroup.alpha = 0f;
        fillImage.fillAmount = 0f;

        // 2. Fade the whole UI in
        _canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);

        // 3. Shoot the red bar from 0 to its current health really quickly!
        float targetFill = currentHealth / maxHealth;

        _healthTween?.Kill(); // Stop any previous health animations
        _healthTween = fillImage.DOFillAmount(targetFill, introFillDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (fillImage != null)
        {
            float targetFill = currentHealth / maxHealth;

            // 4. When taking damage, smoothly drain the bar instead of instantly snapping it
            _healthTween?.Kill();
            _healthTween = fillImage.DOFillAmount(targetFill, damageDropDuration)
                .SetEase(Ease.OutQuad);
        }
    }

    public void HideBossHealthBar()
    {
        if (healthBarContainer == null) return;

        // 5. Fade out completely, THEN disable the GameObject
        _canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            healthBarContainer.SetActive(false);
        });
    }

    private void OnDestroy()
    {
        // Clean up DOTween memory if the scene restarts
        _canvasGroup.DOKill();
        fillImage.DOKill();
    }
}
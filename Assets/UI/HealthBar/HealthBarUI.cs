using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Smooth Drain Settings")]
    [SerializeField] private float drainSpeed = 2f;
    private float targetFillAmount = 1f;

    [Header("Pulse Settings")]
    [SerializeField] private float lowHealthThreshold = 0.3f; // Pulse at 30% health
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pulseColor = new Color(1f, 0.5f, 0.5f, 1f); // Light red glow

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            // Initialize the fill immediately
            targetFillAmount = playerHealth.HealthPercentage;
            healthFillImage.fillAmount = targetFillAmount;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        targetFillAmount = current / max;
    }

    private void Update()
    {
        if (healthFillImage == null) return;

        // 1. Smooth Drain Effect
        // Move the current fill amount toward the target over time
        healthFillImage.fillAmount = Mathf.MoveTowards(
            healthFillImage.fillAmount,
            targetFillAmount,
            Time.deltaTime * drainSpeed
        );

        // 2. Low Health Pulse Effect
        if (targetFillAmount <= lowHealthThreshold)
        {
            // Use a Sine wave to oscillate between normal and pulse colors
            float lerpTime = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            healthFillImage.color = Color.Lerp(normalColor, pulseColor, lerpTime);
        }
        else
        {
            // Reset to normal color if health is recovered
            healthFillImage.color = normalColor;
        }
    }
}
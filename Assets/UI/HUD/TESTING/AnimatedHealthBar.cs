using UnityEngine;

public class AnimatedHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Player GameObject here so we can read their health!")]
    [SerializeField] private PlayerHealth playerHealth;

    private Animator _animator;

    [Header("Settings")]
    [Tooltip("The EXACT name of the animation block inside the Animator window")]
    [SerializeField] private string animationStateName = "pixel_health_bar";

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        // Subscribe to your existing health event!
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateAnimatorFrame;
        }
    }

    private void OnDisable()
    {
        // Clean up the event listener if the UI is destroyed
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateAnimatorFrame;
        }
    }

    private void Start()
    {
        // Force the bar to update the exact moment the game starts
        if (playerHealth != null)
        {
            UpdateAnimatorFrame(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void UpdateAnimatorFrame(float currentHealth, float maxHealth)
    {
        if (_animator == null || maxHealth <= 0) return;

        // 1. Get the health as a decimal between 0.0 and 1.0
        float healthPercent = currentHealth / maxHealth;

        // 2. IMPORTANT CALCULATION:
        // Looking at your screenshots, Frame 1 is FULL (red), and Frame 73 is EMPTY (clear).
        // That means 100% health = Animation Time 0.0. 
        // 0% health = Animation Time 1.0.
        // So, we must invert the health percentage to match the timeline!
        float animationScrubTime = 1f - healthPercent;

        // 3. Force the Animator to instantly jump to that exact percentage of the timeline!
        _animator.Play(animationStateName, 0, animationScrubTime);
    }
}
using UnityEngine;
using DG.Tweening; // --- NEW: Required for smooth sliding! ---

public class AnimatedHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Player GameObject here so we can read their health!")]
    [SerializeField] private PlayerHealth playerHealth;

    private Animator _animator;
    private Tween _currentTween;
    private float _visualHealthPercent = 1f; // Tracks our smooth intermediate position

    [Header("Settings")]
    [Tooltip("The EXACT name of the animation block inside the Animator window")]
    [SerializeField] private string animationStateName = "pixel_health_bar";

    [Tooltip("How long it takes for the bar to slide to its new value")]
    [SerializeField] private float smoothDuration = 0.25f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
        if (_currentTween != null) _currentTween.Kill();
    }

    private void Start()
    {
        if (playerHealth != null && playerHealth.MaxHealth > 0)
        {
            _visualHealthPercent = playerHealth.CurrentHealth / playerHealth.MaxHealth;
            UpdateAnimatorFrame(_visualHealthPercent);
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0) return;

        float targetPercent = currentHealth / maxHealth;

        // Kill any ongoing slide so it doesn't fight the new one
        if (_currentTween != null) _currentTween.Kill();

        // --- THE MAGIC: Smoothly slide our visual tracker over time ---
        _currentTween = DOTween.To(
            () => _visualHealthPercent,
            x => {
                _visualHealthPercent = x;
                UpdateAnimatorFrame(_visualHealthPercent);
            },
            targetPercent,
            smoothDuration
        ).SetEase(Ease.OutQuad).SetUpdate(true); // SetUpdate(true) makes it work even when game time is paused!
    }

    private void UpdateAnimatorFrame(float percent)
    {
        if (_animator == null) return;

        // Invert it because Frame 0 is Full, Frame 73 is Empty
        float animationScrubTime = 1f - Mathf.Clamp01(percent);
        _animator.Play(animationStateName, 0, animationScrubTime);
    }
}
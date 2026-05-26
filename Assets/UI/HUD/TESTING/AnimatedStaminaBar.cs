using UnityEngine;
using DG.Tweening; // --- NEW: Required for smooth sliding! ---

public class AnimatedStaminaBar : MonoBehaviour
{
    private Animator _animator;
    private Tween _currentTween;
    private float _visualStaminaPercent = 1f;

    [Header("Settings")]
    [Tooltip("The EXACT name of the animation block inside the Animator window")]
    [SerializeField] private string animationStateName = "pixel_stamina_bar";

    [Tooltip("Keep this fast (0.1 - 0.15) so stamina tracking feels incredibly precise!")]
    [SerializeField] private float smoothDuration = 0.12f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnDestroy()
    {
        if (_currentTween != null) _currentTween.Kill();
    }

    public void SetStamina(int currentStamina, int maxStamina)
    {
        if (_animator == null || maxStamina <= 0) return;

        float targetPercent = (float)currentStamina / maxStamina;

        
        if (_currentTween != null) _currentTween.Kill();

        
        _currentTween = DOTween.To(
            () => _visualStaminaPercent,
            x => {
                _visualStaminaPercent = x;
                UpdateAnimatorFrame(_visualStaminaPercent);
            },
            targetPercent,
            smoothDuration
        ).SetEase(Ease.Linear).SetUpdate(true); 
    }

    private void UpdateAnimatorFrame(float percent)
    {
        if (_animator == null) return;

        // Clamp to 0.99f to ensure we stay on the last frame of the empty state 
        // and don't accidentally wrap around to the first frame (full state).
        float animationScrubTime = Mathf.Clamp(1f - percent, 0f, 0.99f);
        _animator.Play(animationStateName, 0, animationScrubTime);
    }
}
using UnityEngine;

public class AnimatedStaminaBar : MonoBehaviour
{
    private Animator _animator;

    [Header("Settings")]
    [Tooltip("The EXACT name of the animation block inside the Animator window")]
    [SerializeField] private string animationStateName = "pixel_stamina_bar";

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Your PlayerStamina.cs script already calls this perfectly!
    public void SetStamina(int currentStamina, int maxStamina)
    {
        if (_animator == null || maxStamina <= 0) return;

        // 1. Get the percentage
        float staminaPercent = (float)currentStamina / maxStamina;

        // 2. Invert it so 100% = Frame 1 (Full) and 0% = Frame 45 (Empty)
        float animationScrubTime = 1f - staminaPercent;

        // 3. Snap the animator to that exact frame
        _animator.Play(animationStateName, 0, animationScrubTime);
    }
}

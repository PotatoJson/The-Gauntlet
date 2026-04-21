using UnityEngine;

[RequireComponent(typeof(Animator))]
public class TrapEnemy : MonoBehaviour
{
    [Header("Trap Settings")]
    [Tooltip("Amount of damage to deal when the arm hits the player.")]
    [SerializeField] private float damageAmount = 9999f;
    [Tooltip("Can the trap be triggered multiple times?")]
    [SerializeField] private bool oneShotTrap = true;

    [Tooltip("The trigger parameter name in the Animator.")]
    [SerializeField] private string grabTriggerName = "Grab";

    private Animator animator;
    private bool hasTriggered = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // This is called by the child TrapDetection script
    public void ActivateTrap()
    {
        if (hasTriggered && oneShotTrap) return;

        hasTriggered = true;
        if (animator != null)
        {
            // Activate the animation using a Trigger instead of playing a state
            animator.SetTrigger(grabTriggerName);
        }
    }

    // This handles the arm actually hitting the player and dealing damage
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // Replace 'TakeDamage' with your actual method if it's named differently
                playerHealth.TakeDamage(damageAmount);
            }
        }
    }

    // Optional: Call this from an Animation Event at the end of the trap animation to reset it
    public void ResetTrap()
    {
        hasTriggered = false;
    }
}
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class TrapEnemy : MonoBehaviour
{
    [Header("Trap Settings")]
    [Tooltip("How long targets stay trapped.")]
    [SerializeField] private float trapDuration = 5f;

    [Tooltip("Optional damage on grab (set to 0 for none).")]
    [SerializeField] private float damageAmount = 0f;

    [Tooltip("Can the trap be triggered multiple times?")]
    [SerializeField] private bool oneShotTrap = true;

    [Tooltip("The trigger parameter name in the Animator.")]
    [SerializeField] private string grabTriggerName = "Grab";

    [Header("Grab Hitbox")]
    [SerializeField] private TrapGrabHitbox grabHitbox;

    private Animator animator;
    private bool hasTriggered;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (grabHitbox != null)
        {
            grabHitbox.SetOwner(this);
        }
    }

    public void ActivateTrap()
    {
        if (hasTriggered && oneShotTrap) return;
        Debug.Log($"{gameObject.name} trap activated! for {trapDuration} seconds with {damageAmount} damage.");
        hasTriggered = true;
        animator?.SetTrigger(grabTriggerName);
    }

    public void EnableGrabHitbox()
    {
        grabHitbox?.EnableHitbox();
    }

    public void DisableGrabHitbox()
    {
        grabHitbox?.DisableHitbox();
    }

    public void TryApplyTrap(Collider other)
    {
        if (other == null) return;

        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
        {
            if (damageAmount > 0f)
            {
                PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damageAmount, 0, gameObject);
                }
            }

            playerMovement.ApplyTrap(trapDuration);
            return;
        }

        BaseEnemy enemy = other.GetComponentInParent<BaseEnemy>();
        if (enemy != null)
        {
            enemy.ApplyStun(trapDuration);

            if (damageAmount > 0f)
            {
                enemy.TakeDamage(damageAmount);
            }
        }
    }

    public void ResetTrap()
    {
        hasTriggered = false;
    }
}
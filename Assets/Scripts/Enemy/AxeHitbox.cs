using UnityEngine;

/// <summary>
/// Attach this to the axe GameObject that has a Collider (set as Trigger).
/// The collider starts disabled and is toggled via animation events on the enemy.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AxeHitbox : MonoBehaviour
{
    private Collider hitboxCollider;
    private BaseEnemy ownerEnemy;
    private float pendingDamage;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;

        // Walk up the hierarchy to find the owning enemy
        ownerEnemy = GetComponentInParent<BaseEnemy>();
        if (ownerEnemy == null)
        {
            Debug.LogError($"AxeHitbox on {gameObject.name}: No BaseEnemy found in parent hierarchy!");
        }
    }

    /// <summary>
    /// Call from animation event to activate the hitbox with a specific damage value.
    /// </summary>
    public void EnableHitbox(float damage)
    {
        pendingDamage = damage;
        hitboxCollider.enabled = true;
        Debug.Log($"{ownerEnemy?.gameObject.name}: Axe hitbox enabled (damage={pendingDamage}).");
    }

    /// <summary>
    /// Call from animation event to deactivate the hitbox.
    /// </summary>
    public void DisableHitbox()
    {
        hitboxCollider.enabled = false;
        Debug.Log($"{ownerEnemy?.gameObject.name}: Axe hitbox disabled.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hitboxCollider.enabled) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.Log($"{ownerEnemy?.gameObject.name}: Axe hitbox triggered by {other.name}, but no PlayerHealth found.");
            return;
        }

        GameObject attackerRoot = ownerEnemy != null ? ownerEnemy.gameObject : transform.root.gameObject;
        Debug.Log($"{ownerEnemy?.gameObject.name}: Axe hitbox dealing {pendingDamage} damage to {other.name}.");
        playerHealth.TakeDamage(pendingDamage, 0, attackerRoot);

        Debug.Log($"{ownerEnemy?.gameObject.name}: Axe collider hit player for {pendingDamage} damage!");

        // Disable after hitting so we don't multi-hit in one swing
        hitboxCollider.enabled = false;
    }
}
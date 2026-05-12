using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShieldHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private ShieldEnemy ownerEnemy;

    private float currentHealth;
    private bool isBroken;
    private Collider shieldCollider;

    private void Awake()
    {
        shieldCollider = GetComponent<Collider>();
        shieldCollider.isTrigger = true;

        if (ownerEnemy == null)
        {
            ownerEnemy = GetComponentInParent<ShieldEnemy>();
        }

        currentHealth = maxHealth;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBroken) return;

        if (!other.TryGetComponent<HitboxController>(out HitboxController hitbox)) return;

        Debug.Log($"{gameObject.name}: Shield hit for {hitbox.CurrentDamage} damage by {other.name}.");
        ApplyDamage(hitbox.CurrentDamage);
        hitbox.DisableCollider();
    }

    private void ApplyDamage(float damage)
    {
        if (isBroken) return;

        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            BreakShield();
        }
    }

    private void BreakShield()
    {
        if (isBroken) return;

        isBroken = true;
        shieldCollider.enabled = false;
        ownerEnemy?.OnShieldBroken();
        Destroy(gameObject);
    }
}
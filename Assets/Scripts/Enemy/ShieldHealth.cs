using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShieldHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private ShieldEnemy ownerEnemy;

    [Header("Visual Effects")]
    public GameObject shieldHitVfxPrefab;

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

        if (shieldHitVfxPrefab != null)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - other.transform.position).normalized;
            hitNormal += new Vector3(0, 0.5f, 0); // Angle it slightly upwards
            
            GameObject vfx = Instantiate(shieldHitVfxPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            Destroy(vfx, 2f); // Clean up sparks after 2 seconds
        }

        RegisterShieldHit(hitbox);
        Debug.Log($"{gameObject.name}: Shield hit for {hitbox.CurrentDamage} damage by {other.name}.");
        ApplyDamage(hitbox.CurrentDamage);
        hitbox.DisableCollider();
    }

    public void RegisterShieldHit(HitboxController hitbox)
    {
        ownerEnemy?.RegisterShieldHit(hitbox);
    }

    public bool IsHitboxBlocked(Collider hitboxCollider)
    {
        if (isBroken || shieldCollider == null || !shieldCollider.enabled || hitboxCollider == null) return false;

        return shieldCollider.bounds.Intersects(hitboxCollider.bounds);
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
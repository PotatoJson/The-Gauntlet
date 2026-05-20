using UnityEngine; 

[RequireComponent(typeof(Rigidbody))]
public abstract class BaseSkillProjectile : MonoBehaviour
{
    [Header("Base Projectile Stats")]
    public float Speed = 20f;
    public float Lifetime = 5f;
    
    [Tooltip("Multiplier for the player's base damage (e.g., 1.5 = 150% damage)")]
    public float DamageMultiplier = 1f; 

    protected float _calculatedDamage;
    protected int _calculatedPoise;
    
    public virtual void Initialize(float playerDamage, float playerPoise)
    {
        _calculatedDamage = playerDamage * DamageMultiplier;
        _calculatedPoise = Mathf.RoundToInt(playerPoise * DamageMultiplier);
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * Speed; 
        }

        Destroy(gameObject, Lifetime);
    }

    protected abstract void OnTriggerEnter(Collider other);
}

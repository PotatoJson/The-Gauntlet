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
    protected Transform _target;
    protected Rigidbody _rb;
    
    public virtual void Initialize(float playerDamage, float playerPoise, Transform target = null)
    {
        _calculatedDamage = playerDamage * DamageMultiplier;
        _calculatedPoise = Mathf.RoundToInt(playerPoise * DamageMultiplier);
        _target = target;
        _rb = GetComponent<Rigidbody>();

        // This is the default targetting system for Physical, Ice, and Lightning projectiles
        if (_target != null)
        {
            Vector3 aimPoint = _target.position + (Vector3.up * 1.4f);
            
            float flatDistance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(aimPoint.x, aimPoint.z)
            );
            //make sure enemy is far enough away to actually use the aiming math
            if (flatDistance < 3f)
            {
                Vector3 flatForward = transform.forward;
                flatForward.y = 0;
                if (flatForward != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(flatForward);
                }
            }
            else
            {
                transform.LookAt(aimPoint);
            }
        }
        
        if (_rb != null)
        {
            _rb.linearVelocity = transform.forward * Speed; 
        }

        Destroy(gameObject, Lifetime);
    }

    protected abstract void OnTriggerEnter(Collider other);
}

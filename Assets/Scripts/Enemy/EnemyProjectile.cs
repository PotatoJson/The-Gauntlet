using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float lifetime = 5f;

    private Collider _projectileCollider;
    private GameObject _instigator;
    private Vector3 _moveDirection = Vector3.forward;

    private void Awake()
    {
        _projectileCollider = GetComponent<Collider>();
        _moveDirection = transform.forward;
    }

    private void Start()
    {
        Debug.Log($"[EnemyProjectile] Spawned: {name} at {transform.position} (lifetime={lifetime})");
        // Destroy the fireball after a few seconds so it doesn't clutter the scene forever if it misses
        Destroy(gameObject, lifetime);
    }

    public void Initialize(GameObject owner)
    {
        if (owner == null || _projectileCollider == null) return;

        _instigator = owner;

        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>();
        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider != null)
            {
                Physics.IgnoreCollision(_projectileCollider, ownerCollider);
            }
        }
    }

    public void SetDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        _moveDirection = direction.normalized;
        transform.rotation = Quaternion.LookRotation(_moveDirection);
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    private void Update()
    {
        transform.position += _moveDirection * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[EnemyProjectile] Trigger enter: {name} hit {other.name} (tag={other.tag}, isTrigger={other.isTrigger})");

        // Check if we hit the player (supports child colliders)
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            Debug.Log($"[EnemyProjectile] Damaging player for {damage} and destroying projectile.");
            playerHealth.TakeDamage(damage, 0, _instigator);
            Destroy(gameObject);
            return;
        }

        // Destroy if it hits environment like a wall or floor (ignores enemies and other triggers)
        if (!other.CompareTag("BasicEnemy") && !other.CompareTag("EliteEnemy") && !other.isTrigger)
        {
            Debug.Log("[EnemyProjectile] Hit environment, destroying projectile.");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Debug.Log($"[EnemyProjectile] Destroyed: {name}");
    }
}
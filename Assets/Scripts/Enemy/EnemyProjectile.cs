using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float lifetime = 10f;

    [Header("Impact")]
    [SerializeField] private ImpactFx impactPrefab;
    [SerializeField] private float impactRadius = 1.2f;

    /// <summary>Off for the Caster's fireballs: it plays its own landing burst where they hit the ground.</summary>
    [System.NonSerialized] public bool spawnImpactOnHit = true;

    /// <summary>On for the Eyeball's fireballs, so they burst on walls instead of flying through them.</summary>
    [System.NonSerialized] public bool stopOnEnvironment;

    private Collider _projectileCollider;
    private GameObject _instigator;
    private Vector3 _moveDirection = Vector3.forward;
    private float _age;
    private bool _burst;

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
        _age += Time.deltaTime;
        transform.position += _moveDirection * speed * Time.deltaTime;
    }

    private void Burst()
    {
        if (_burst) return;
        _burst = true;

        if (!spawnImpactOnHit || impactPrefab == null) return;

        ImpactFx fx = Instantiate(impactPrefab, transform.position, Quaternion.identity);
        fx.Play(impactRadius);
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
            Burst();
            Destroy(gameObject);
            return;
        }

        // Walls and floors stop it (never other enemies, and not in the first instant while it leaves the shooter).
        if (stopOnEnvironment && _age > 0.15f && !other.isTrigger && other.GetComponentInParent<BaseEnemy>() == null)
        {
            Burst();
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Debug.Log($"[EnemyProjectile] Destroyed: {name}");
    }
}
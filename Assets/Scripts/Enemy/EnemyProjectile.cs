using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float lifetime = 5f;

    private void Start()
    {
        // Destroy the fireball after a few seconds so it doesn't clutter the scene forever if it misses
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // The EyeEnemy spawns the fireball already rotated to face the player.
        // This makes it continuously fly straight ahead in that direction.
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if we hit the player (supports child colliders)
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Destroy if it hits environment like a wall or floor (ignores enemies and other triggers)
        if (!other.CompareTag("Enemy") && !other.CompareTag("EliteEnemy") && !other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
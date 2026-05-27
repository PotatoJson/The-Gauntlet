using UnityEngine;

public class WindProjectile : BaseSkillProjectile
{
    [Header("Wind Projectile specifics")]
    public GameObject HitVfx;
    public float PullRadius;
    public float PullSpeed;
    public float DamageTickRate;
    public float DamagePerTickMultiplier;

    private float _tickTimer;

    protected override void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        _tickTimer += Time.deltaTime;
        bool shouldTickDamage = _tickTimer >= DamageTickRate;
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, PullRadius);

        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player")) continue;
            if (hit.TryGetComponent<BaseEnemy>(out BaseEnemy enemy))
            {
                Vector3 directionToCenter = (transform.position - enemy.transform.position).normalized;
                directionToCenter.y = 0; 
                enemy.transform.position += directionToCenter * PullSpeed * Time.deltaTime;

                if (shouldTickDamage)
                {
                    float tickDamage = _calculatedDamage * DamagePerTickMultiplier;
                    enemy.TakeDamage(tickDamage);
                }
            }
        }

        if (shouldTickDamage)
        {
            _tickTimer = 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f); // Cyan transparent
        Gizmos.DrawSphere(transform.position, PullRadius);
    }
}

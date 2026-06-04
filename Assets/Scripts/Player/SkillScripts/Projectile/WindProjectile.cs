using UnityEngine;
using System.Collections.Generic;

public class WindProjectile : BaseSkillProjectile
{
    [Header("Wind Projectile specifics")]
    public GameObject HitVfx;
    public float PullRadius;
    public float PullSpeed;
    public float DamageTickRate;
    public float DamagePerTickMultiplier;

    private float _tickTimer;

    public override void Initialize(float playerDamage, float playerPoise, Transform target = null)
    {
        base.Initialize(playerDamage, playerPoise, target);

        Vector3 flatForward = transform.forward;
        flatForward.y = 0; 

        if (_rb != null)
        {
            _rb.linearVelocity = flatForward.normalized * Speed;
        }
    }

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

        HashSet<GameObject> hitThisTick = new HashSet<GameObject>();

        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player")) continue;
            if (hit.TryGetComponent<BaseEnemy>(out BaseEnemy enemy))
            {
                Vector3 directionToCenter = (transform.position - enemy.transform.position).normalized;
                directionToCenter.y = 0; 

                float actualPullSpeed = PullSpeed / Mathf.Max(0.1f, enemy.weight);
                enemy.transform.position += directionToCenter * actualPullSpeed * Time.deltaTime;

                if (shouldTickDamage)
                {
                    GameObject enemyRoot = enemy.transform.root.gameObject;
                    
                    // Only apply damage if haven't already hit this enemy during this specific tick
                    if (hitThisTick.Add(enemyRoot)) 
                    {
                        float tickDamage = _calculatedDamage * DamagePerTickMultiplier;
                        enemy.TakeDamage(tickDamage);
                        if (HitVfx != null) Instantiate(HitVfx, enemy.transform.position, Quaternion.identity);
                    }
                    
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

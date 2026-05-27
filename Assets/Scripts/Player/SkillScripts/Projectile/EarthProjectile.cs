using UnityEngine;
using System.Collections.Generic;

public class EarthProjectile : BaseSkillProjectile
{
    [Header("The Throw")]
    public float UpwardThrowForce = 5f; // to make this feel like a throw instead of just spawning and moving

    [Header("DirectHit Things")]
    public float KnockbackForce = 15f;
    //public int ExtraPoiseDmg = 30; when ever enemies can handle the incoming poise dmg

    [Header("Shrapnel Blast AOE")]
    public float AoERadius = 2.5f;
    [Range(0f, 1f)] public float AoEDamagePercentage = 0.3f; //30% of initial hit dmg

    [Header("Visuals")]
    public GameObject ImpactVFX;

    public override void Initialize(float playerDamage, float playerPoise, Transform target = null)
    {
        base.Initialize(playerDamage, playerPoise, target);

        if (_rb != null)
        {
            _rb.useGravity = true; 
            _rb.linearVelocity = (transform.forward * Speed) + (Vector3.up * UpwardThrowForce);
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
        Debug.Log($"<color=orange>[PROJECTILE DEBUG]</color> {gameObject.name} collided with: <b>{other.gameObject.name}</b> | Tag: {other.tag} | Layer: {LayerMask.LayerToName(other.gameObject.layer)}");
        if (other.CompareTag("Player")) return;
        if (ImpactVFX != null) Instantiate(ImpactVFX, transform.position, Quaternion.identity);

        HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

        if (other.TryGetComponent<BaseEnemy>(out BaseEnemy directEnemy))
        {
            GameObject directEnemyRoot = other.transform.root.gameObject;
            alreadyHit.Add(directEnemyRoot);

            directEnemy.TakeDamage(_calculatedDamage);

            // If you add a knockback function to your enemies later
            // directEnemy.ApplyKnockback(transform.forward * KnockbackForce);
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, AoERadius);
        
        float aoeDamage = _calculatedDamage * AoEDamagePercentage;

        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player")) continue;

            if (hit.TryGetComponent<BaseEnemy>(out BaseEnemy splashEnemy))
            {
                GameObject splashEnemyRoot = hit.transform.root.gameObject;
                
                if (alreadyHit.Add(splashEnemyRoot))
                {
                    splashEnemy.TakeDamage(aoeDamage);
                }
            }
        }
        Destroy(gameObject);
    }
}

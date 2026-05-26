using UnityEngine;
using System.Collections.Generic;

public class FireballProjectile : BaseSkillProjectile
{
    [Header("Fire Specifics")]
    public float ExplosionRadius = 5f;
    public GameObject ExplosionVFX;

    public float BurnDps = 5f;
    public float BurnDuration = 4f;
    
    protected override void OnTriggerEnter(Collider other)
    {
        Debug.Log($"<color=orange>[PROJECTILE DEBUG]</color> {gameObject.name} collided with: <b>{other.gameObject.name}</b> | Tag: {other.tag} | Layer: {LayerMask.LayerToName(other.gameObject.layer)}");
        if (other.CompareTag("Player")) return;

        if (ExplosionVFX != null) Instantiate(ExplosionVFX, transform.position, Quaternion.identity);
        HashSet<GameObject> alreadyHit = new HashSet<GameObject>();
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, ExplosionRadius);

        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player")) continue;

            if (hit.TryGetComponent<BaseEnemy>(out BaseEnemy enemyScript))
            {
                // Group by root object
                GameObject enemyRoot = hit.transform.root.gameObject;
                
                if (alreadyHit.Add(enemyRoot))
                {
                    enemyScript.TakeDamage(_calculatedDamage);
                    if (enemyScript.TryGetComponent<StatusManager>(out StatusManager statusManager))
                    {
                        statusManager.ApplyStatus(new BurnStatus(BurnDps, BurnDuration));
                    }

                    if (MetricsTracker.Instance != null)
                    {
                        //if we want to track fire ball things
                    }
                }
            }
        }
        Destroy(gameObject);
    }
}

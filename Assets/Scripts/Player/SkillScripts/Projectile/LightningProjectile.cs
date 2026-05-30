using UnityEngine;
using System.Collections.Generic;

public class LightningProjectile : BaseSkillProjectile
{
    [Header("Lightning Chain Settings")]
    public int maxChains = 3;
    public float chainRadius = 8f;
    public float microStunDuration = 0.5f;
    public GameObject lightningHitVFX; 

    private HashSet<GameObject> _alreadyHit = new HashSet<GameObject>();
    private int _chainsCompleted = 0;

    protected override void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return;

        if (other.TryGetComponent<BaseEnemy>(out BaseEnemy initialEnemy))
        {
            GameObject enemyRoot = other.transform.root.gameObject;
            if (_alreadyHit.Add(enemyRoot))
            {
                StrikeEnemy(initialEnemy, ((Vector3.up * 1.5f) + other.transform.position));
                if (_chainsCompleted < maxChains)
                {
                    BaseEnemy nextTarget = FindNextTarget(transform.position);
                    if (nextTarget != null)
                    {
                        _chainsCompleted++;
                        
                        // Re-aim the projectile at the new target
                        Vector3 aimDir = (nextTarget.transform.position + Vector3.up * 1f) - transform.position;
                        transform.rotation = Quaternion.LookRotation(aimDir);
                        
                        if (_rb != null) _rb.linearVelocity = transform.forward * Speed;
                        
                        return;
                    }
                }
                Destroy(gameObject);
            }
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            Destroy(gameObject);
        }
    }

    private void StrikeEnemy(BaseEnemy enemy, Vector3 hitPos)
    {
        enemy.TakeDamage(_calculatedDamage);
        
        // --- NEW CONNECTION: Apply the Stun Status ---
        if (enemy.TryGetComponent<StatusManager>(out StatusManager statusManager))
        {
            statusManager.ApplyStatus(new JoltedStatus(microStunDuration));
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[STATUS]</color> {enemy.name} is missing a StatusManager!");
        }
        
        if (lightningHitVFX != null) Instantiate(lightningHitVFX, hitPos, Quaternion.identity);
    }

    private BaseEnemy FindNextTarget(Vector3 pos)
    {
        Collider[] colliders = Physics.OverlapSphere(pos, chainRadius);
        BaseEnemy closestEnemy = null;
        float closestDist = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player")) continue;
            if (col.TryGetComponent<BaseEnemy>(out BaseEnemy enemy))
            {
                if (!_alreadyHit.Contains(enemy.transform.root.gameObject) && !enemy.IsDead())
                {
                    float dist = Vector3.Distance(pos, enemy.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestEnemy = enemy;
                    }
                }
            }
        }
        return closestEnemy;
    }

}

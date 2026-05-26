using UnityEngine;
using System.Collections.Generic;

public class IceProjectile : BaseSkillProjectile
{
    [Header("Ice Settings")]
    [Tooltip("What percentage of their speed is left? (0.5 = 50% speed)")]
    public float slowMultiplier = 0.5f;
    public float slowDuration = 3f;
    public GameObject pierceVFX;

    private HashSet<GameObject> _alreadyHit = new HashSet<GameObject>();

    protected override void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return;
        if (other.TryGetComponent<BaseEnemy>(out BaseEnemy enemyScript))
        {
            GameObject enemyRoot = other.transform.root.gameObject;
            
            if (_alreadyHit.Add(enemyRoot))
            {
                enemyScript.TakeDamage(_calculatedDamage);
                if (enemyScript.TryGetComponent<StatusManager>(out StatusManager statusManager))
                {
                    statusManager.ApplyStatus(new ChilledStatus(slowMultiplier, slowDuration));
                }
                else
                {
                    Debug.LogWarning($"<color=yellow>[STATUS]</color> {enemyScript.name} is missing a StatusManager component!");
                }

                if (pierceVFX != null)
                {
                    Instantiate(pierceVFX, other.transform.position, Quaternion.identity);
                }
                
            }
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            Destroy(gameObject);
        }
    }
}

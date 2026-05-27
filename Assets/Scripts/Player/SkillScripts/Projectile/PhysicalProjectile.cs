using UnityEngine;
using System.Collections.Generic;

public class PhysicalProjectile : BaseSkillProjectile
{
    private int _pierceCount = 0;
    public int MaxPierces = 2;
    [Header("Shockwave Specifics")]
    [Tooltip("Visual effect to spawn when slicing through an enemy")]
    public GameObject PierceVFX; 

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
                _pierceCount++;

                if(_pierceCount >= MaxPierces)
                {
                    Destroy(gameObject);
                }
                if (PierceVFX != null)
                {
                    Instantiate(PierceVFX, other.transform.position, Quaternion.identity);
                }
            }
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            Destroy(gameObject);
        }
    }
}

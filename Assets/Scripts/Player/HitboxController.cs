using UnityEngine;
using System.Collections.Generic;

public class HitboxController : MonoBehaviour
{
    public GameObject bloodEffectPrefab;
    private Collider _collider;


    //states passed from combat manager
    public int CurrentDamage => _currentDamage;
    private int _currentDamage;
    private int _currentPoiseDamage;
    private CombatInput _currentAttackType;

    private HashSet<GameObject> _alreadyHit = new HashSet<GameObject>();

    private void Awake()
    {
        /*if(bloodEffectPrefab == null)
        {
            Debug.LogError("Blood effect prefab not assigned in HitboxController.");
        }*/
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    public void EnableCollider(int damage, int poise, CombatInput attackType)
    {
        Debug.Log("hitbox enabled");
        _alreadyHit.Clear();
        _currentDamage = damage;
        _currentPoiseDamage = poise;
        _currentAttackType = attackType;
        _collider.enabled = true;
    }

    public void DisableCollider()
    {
        Debug.Log("hitbox Disabled");
        _collider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.transform.root == transform.root) return;
        if(other.TryGetComponent<BaseEnemy>(out BaseEnemy enemyScript))
        {
            GameObject enemyRoot = other.transform.root.gameObject;
            if(_alreadyHit.Add(enemyRoot))
            {
                enemyScript.TakeDamage(_currentDamage/*, _currentPoiseDamage TODO: Add poise system to enemies later*/);
                SpawnBlood(other);

                if (MetricsTracker.Instance != null)
                {
                    MetricsTracker.Instance.RecordMeleeHit(_currentAttackType, _currentDamage);
                }
            }
        }
        Debug.Log($"Hit {other.name} for {_currentDamage} Damage");
    }

    private void SpawnBlood(Collider target)
    {
        Vector3 spawnPosition = target.ClosestPoint(transform.position);
        
        Vector3 punchDirection = (target.transform.position - transform.position).normalized;
        punchDirection += new Vector3(Random.Range(-0.1f, 0.1f), 1.1f, Random.Range(-0.1f, 0.1f));
        Quaternion rotation = Quaternion.LookRotation(punchDirection);

        GameObject bloodEffect = Instantiate(bloodEffectPrefab, spawnPosition, rotation);

        Destroy(bloodEffect, 3f);
    }
}

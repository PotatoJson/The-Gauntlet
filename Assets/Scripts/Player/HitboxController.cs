using UnityEngine;
using System.Collections.Generic;

public class HitboxController : MonoBehaviour
{
    private Collider _collider;

    //states passed from combat manager
    private int _currentDamage;
    private int _currentPoiseDamage;

    private HashSet<GameObject> _alreadyHit = new HashSet<GameObject>();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    public void EnableCollider(int damage, int poise)
    {
        _alreadyHit.Clear();
        _currentDamage = damage;
        _currentPoiseDamage = poise;
        _collider.enabled = true;
    }

    public void DisableCollider()
    {
        _collider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        /*if(other.CompareTag("BasicEnemy") && !_alreadyHit.Contains(other))
        {
            _alreadyHit.Add(other);
            //TODO generate combatPacket
            //and actually make the enemy take damage
            Debug.Log($"Hit {other.name} for {_currentDamage} Damage and {_currentPoiseDamage} poise");
        }*/
        if(other.transform.root == transform.root) return;
        if(other.TryGetComponent<BaseEnemy>(out BaseEnemy enemyScript))
        {
            GameObject enemyRoot = other.transform.root.gameObject;
            if(_alreadyHit.Add(enemyRoot))
            {
                enemyScript.TakeDamage(_currentDamage/*, _currentPoiseDamage TODO: Add poise system to enemies later*/);
            }
        }
        Debug.Log($"Hit {other.name} for {_currentDamage} Damage and {_currentPoiseDamage} poise");
    }
}

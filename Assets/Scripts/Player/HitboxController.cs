using UnityEngine;
using System.Collections.Generic;

public class HitboxController : MonoBehaviour
{
    private Collider _collider;

    //states passed from combat manager
    private int _currentDamage;
    private int _currentPoiseDamage;

    private List<Collider> _alreadyHit = new List<Collider>();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    public void EnableCollider(int damage, int poise)
    {
        _currentDamage = damage;
        _currentPoiseDamage = poise;
        _alreadyHit.Clear();
        _collider.enabled = true;
    }

    public void DisableCollider()
    {
        _collider.enabled = false;
    }

    /*private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Enemy") && !_alreadyHit.Contains(other))
        {
            _alreadyHit.Add(other);
            //TODO generate combatPacket
            //and actually make the enemy take damage
            Debug.Log($"Hit {other.name} for {_currentDamage} Damage and {_currentPoiseDamage} poise");
        }
    }*/
}

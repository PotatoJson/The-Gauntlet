using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class TrapGrabHitbox : MonoBehaviour
{
    private Collider _hitboxCollider;
    private TrapEnemy _owner;
    private readonly HashSet<GameObject> _alreadyHit = new HashSet<GameObject>();

    private void Awake()
    {
        _hitboxCollider = GetComponent<Collider>();
        _hitboxCollider.isTrigger = true;
        _hitboxCollider.enabled = false;
    }

    public void SetOwner(TrapEnemy owner)
    {
        _owner = owner;
    }

    public void EnableHitbox()
    {
        _alreadyHit.Clear();
        _hitboxCollider.enabled = true;
    }

    public void DisableHitbox()
    {
        _hitboxCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_hitboxCollider.enabled || _owner == null) return;
        if (other.transform.root == transform.root) return;

        GameObject hitRoot = other.transform.root.gameObject;
        if (!_alreadyHit.Add(hitRoot)) return;

        _owner.TryApplyTrap(other);
    }
}
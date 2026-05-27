using UnityEngine;
using System.Collections.Generic;

public class FireballProjectile : BaseSkillProjectile
{
    [Header("Fire Movement")]
    public float MaxSpeed = 25f;
    public float Acceleration = 15f;
    public float TrackingTurnSpeed = 5f;

    [Header("Fire Specifics")]
    public float ExplosionRadius = 5f;
    public GameObject ExplosionVFX;

    public float BurnDps = 5f;
    public float BurnDuration = 4f;
    private float _currentSpeed;
    
    public override void Initialize(float playerDamage, float playerPoise, Transform target)
    {
        base.Initialize(playerDamage, playerPoise, target);
        _currentSpeed = Speed;
    }

    private void FixedUpdate()
    {
        if(_rb == null) return;

        _currentSpeed = Mathf.Min(_currentSpeed + Acceleration * Time.fixedDeltaTime, MaxSpeed);

        if (_target != null)
        {
            Vector3 aimPoint = _target.position + (Vector3.up * 1f);
            Vector3 directionToTarget = (aimPoint - transform.position).normalized;
            
            // Smoothly rotate toward the target
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            _rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, TrackingTurnSpeed * Time.fixedDeltaTime));
        }

        _rb.linearVelocity = transform.forward * _currentSpeed;
    }

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

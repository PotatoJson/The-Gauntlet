using UnityEngine;
using System.Collections.Generic;

public class FireballProjectile : BaseSkillProjectile
{
    [Header("Fire Movement")]
    public float MaxSpeed = 10f;
    public float Acceleration = 5f;
    public float TrackingTurnSpeed = 5f;
    public float AutoTargetRadius = 30f;

    [Header("Fire Ball Expansion")]
    public float MaxScaleMult = 3f;
    public float GrowthDuration = 2f;
    public float SpawnHeightOffset = 2.5f;

    [Header("Fire Specifics")]
    public float ExplosionRadius = 5f;
    public GameObject ExplosionVFX;
    public GameObject FeedingVFXPrefab; //attached to hand

    public float BurnDps = 5f;
    public float BurnDuration = 4f;
    private float _currentSpeed;

    private float _growthTimer = 0f;
    private Vector3 _initialScale;
    private Vector3 _initialPosition; 
    private Vector3 _targetPosition;  
    
    public override void Initialize(float playerDamage, float playerPoise, Transform target)
    {
        base.Initialize(playerDamage, playerPoise, target);
        transform.position = transform.position + (Vector3.up * .4f);
        _initialPosition = transform.position;
        _targetPosition = transform.position + (Vector3.up * SpawnHeightOffset);

        _currentSpeed = Speed;
        _initialScale = transform.localScale;

        if (_target == null)
        {
            _target = FindClosestEnemy(transform.position, AutoTargetRadius);
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
        }
    }

    //player combat will use this eventually to attache fire vfx to hand
    public void AttachFeedingVFX(Transform handTransform)
    {
        if (FeedingVFXPrefab != null && handTransform != null)
        {
            GameObject feedingVFX = Instantiate(FeedingVFXPrefab, handTransform.position, transform.rotation, handTransform);
            Destroy(feedingVFX, GrowthDuration);
        }
    }

    private void FixedUpdate()
    {
        if(_rb == null) return;

        if(_growthTimer < GrowthDuration)
        {
            _growthTimer += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(_growthTimer / GrowthDuration);
            transform.localScale = Vector3.Lerp(_initialScale, _initialScale * MaxScaleMult, progress);
            
            // NEW: Lerp Position upwards
            transform.position = Vector3.Lerp(_initialPosition, _targetPosition, progress);

            _rb.linearVelocity = Vector3.zero;
            return;
        }

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

    private Transform FindClosestEnemy(Vector3 center, float radius)
    {
        Collider[] hitColliders = Physics.OverlapSphere(center, radius);
        Transform closest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider hit in hitColliders)
        {
            if (hit.TryGetComponent<BaseEnemy>(out BaseEnemy enemy))
            {
                float dist = Vector3.Distance(center, hit.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = hit.transform;
                }
            }
        }
        return closest;
    }
}

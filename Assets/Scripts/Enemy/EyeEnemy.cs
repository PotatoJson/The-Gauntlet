using UnityEngine;

public class EyeEnemy : BaseEnemy
{
    [Header("Eye Enemy Settings")]
    [SerializeField] private float preferredDistance = 10f;
    [SerializeField] private float fireballCooldown = 5f;

    [Header("Projectile")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Transform fireballSpawnPoint;

    private float fireballTimer = 0f;
    private float aiDecisionTimer;
    private float aiDecisionInterval = 0.4f;

    // Animation hash for shooting (matching base class light attack if applicable, or custom)
    protected static readonly int AnimShoot = Animator.StringToHash("LightAttack");

    protected override void Start()
    {
        base.Start();
        // Immediately tell the base class we've "opened" so it never forces a charge attack on us
        hasOpenedWithCharge = true;
    }

    protected override void Update()
    {
        base.Update();

        if (IsDead() || isStunned || isInHitStun) return;

        // Don't do anything until aware and engaged
        if (!isAware || !isEngaged) return;

        // Tick down our custom 5-second cooldown timer
        if (fireballTimer > 0f)
        {
            fireballTimer -= Time.deltaTime;
        }

        if (isAttacking) return;

        aiDecisionTimer -= Time.deltaTime;
        if (aiDecisionTimer <= 0f)
        {
            aiDecisionTimer = aiDecisionInterval + Random.Range(-0.1f, 0.1f);
            MakeDecision();
        }
    }

    /// <summary>
    /// Override to prevent BaseEnemy from forcing standard melee engagement.
    /// </summary>
    protected override void ContinueCombat()
    {
        // Leaving this blank stops the base combat loop (which forces Light/Heavy attacks).
        // It's handled entirely by MakeDecision() for this ranged enemy.
    }

    /// <summary>
    /// Override the engagement check so we never attempt an opener charge attack
    /// </summary>
    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= engagementRange)
        {
            isEngaged = true;
            hasOpenedWithCharge = true; // skip opener charge entirely
        }
        else if (isAware && !isAttacking && !isCharging)
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    private void MakeDecision()
    {
        if (isAttacking || isStunned) return;

        // Ask for permission from the EnemyCombatManager if you are using it to regulate attacks
        if (EnemyCombatManager.Instance != null &&
            !EnemyCombatManager.Instance.RequestAttackPermission(this))
        {
            // If we can't attack, just maintain distance and look at the player
            MaintainDistance();
            return;
        }

        float distance = GetDistanceToPlayer();

        if (distance > preferredDistance)
        {
            ChasePlayer();
            FacePlayer();
        }
        else
        {
            // Within preferred range
            if (navAgent != null)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
            }
            FacePlayerImmediate();

            if (fireballTimer <= 0f)
            {
                ShootFireball();
            }
        }
    }

    private void MaintainDistance()
    {
        float distance = GetDistanceToPlayer();
        if (distance > preferredDistance)
        {
            ChasePlayer();
        }
        else if (navAgent != null)
        {
            navAgent.isStopped = true;
        }
        FacePlayer();
    }

    private void ShootFireball()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        fireballTimer = fireballCooldown;
        if (navAgent != null) navAgent.isStopped = true;

        if (animator != null)
        {
            // Trigger animation. You can hook an Animation Event to 'SpawnFireballProjectile' to fire it mid-animation.
            animator.SetTrigger(AnimShoot);
        }
        else
        {
            // Fallback if there is no animator setup
            SpawnFireballProjectile();
            Invoke(nameof(OnRangedAttackEnd), 0.5f);
        }
    }

    /// <summary>
    /// Call this via an Animation Event roughly when the eye actually releases the fireball 
    /// so it syncs up properly with the visual animation.
    /// </summary>
    public void SpawnFireballProjectile()
    {
        if (fireballPrefab != null && fireballSpawnPoint != null)
        {
            // Calculate direction to player aiming slightly upwards or at center mass
            Vector3 directionToPlayer = (player.position + Vector3.up * 1.5f) - fireballSpawnPoint.position;
            Quaternion rotationToPlayer = Quaternion.LookRotation(directionToPlayer);

            Instantiate(fireballPrefab, fireballSpawnPoint.position, rotationToPlayer);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Fireball Prefab or Spawn Point is missing!");
        }
    }

    /// <summary>
    /// Call this via an Animation Event at the very end of the shoot animation.
    /// </summary>
    public void OnRangedAttackEnd()
    {
        OnAttackEnd();
    }
}
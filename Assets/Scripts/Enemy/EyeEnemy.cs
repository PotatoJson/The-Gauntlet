using UnityEngine;

public class EyeEnemy : BaseEnemy
{
    [Header("Eye Enemy Settings")]
    [SerializeField] private float preferredDistance = 10f;
    [SerializeField] private float fireballCooldown = 5f;
    [SerializeField] private float rangedAttackDuration = 0.6f;
    [SerializeField] private float recoilDistance = 2f;
    [SerializeField] private float recoilDuration = 0.15f;

    [Header("Projectile")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Transform fireballSpawnPoint;

    private float fireballTimer = 0f;
    private float aiDecisionTimer;
    private float aiDecisionInterval = 0.4f;
    private Coroutine recoilRoutine;
    private bool hasFiredThisAttack;
    private Coroutine deathRoutine;

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
        hasFiredThisAttack = false;
        fireballTimer = fireballCooldown;
        if (navAgent != null) navAgent.isStopped = true;

        CancelInvoke(nameof(OnRangedAttackEnd));
        Invoke(nameof(OnRangedAttackEnd), Mathf.Max(0.05f, rangedAttackDuration));

        if (animator != null)
        {
            // Trigger animation. You can hook an Animation Event to 'SpawnFireballProjectile' to fire it mid-animation.
            animator.SetTrigger(AnimShoot);
        }
        else
        {
            // Fallback if there is no animator setup
            SpawnFireballProjectile();
        }
    }

    /// <summary>
    /// Call this via an Animation Event roughly when the eye actually releases the fireball 
    /// so it syncs up properly with the visual animation.
    /// </summary>
    public void SpawnFireballProjectile()
    {
        if (hasFiredThisAttack) return;
        hasFiredThisAttack = true;

        if (fireballPrefab != null && fireballSpawnPoint != null)
        {
            // Calculate direction to player aiming slightly upwards or at center mass
            Vector3 directionToPlayer = (player.position + Vector3.up * 1.5f) - fireballSpawnPoint.position;
            if (directionToPlayer.sqrMagnitude < 0.0001f)
            {
                directionToPlayer = transform.forward;
            }

            Quaternion rotationToPlayer = Quaternion.LookRotation(directionToPlayer);

            GameObject fireballObject = Instantiate(fireballPrefab, fireballSpawnPoint.position, rotationToPlayer);

            EnemyProjectile projectile = fireballObject.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(gameObject);
                projectile.SetDirection(directionToPlayer);
            }

            ApplyRecoil();
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Fireball Prefab or Spawn Point is missing!");
        }
    }

    private void ApplyRecoil()
    {
        if (recoilDistance <= 0f || recoilDuration <= 0f) return;

        Vector3 recoilOffset = -transform.forward * recoilDistance;

        if (recoilRoutine != null)
        {
            StopCoroutine(recoilRoutine);
        }

        recoilRoutine = StartCoroutine(ApplyRecoilRoutine(recoilOffset));
    }

    private System.Collections.IEnumerator ApplyRecoilRoutine(Vector3 recoilOffset)
    {
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + recoilOffset;
        float elapsed = 0f;

        while (elapsed < recoilDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoilDuration);
            float easedT = t * t * (3f - 2f * t);
            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, easedT);

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.Move(newPosition - transform.position);
                navAgent.nextPosition = transform.position;
            }
            else
            {
                transform.position = newPosition;
            }

            yield return null;
        }

        recoilRoutine = null;
    }

    protected override void Die()
    {
        if (deathRoutine != null) return;

        StopAllCoroutines();
        isAttacking = false;
        isCharging = false;
        isRecovering = false;
        isInHitStun = false;
        isHitImmune = false;

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.enabled = false;
        }

        animator?.SetTrigger(AnimDie);
        gameObject.layer = LayerMask.NameToLayer("Default");

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        var healthBar = GetComponentInChildren<EnemyHealthBar>();
        if (healthBar != null) healthBar.gameObject.SetActive(false);

        if (animator == null)
        {
            Destroy(gameObject, 3f);
        }
    }

    public void OnDieAnimationEnd()
    {
        Destroy(gameObject);
    }

    /// <summary>
    /// Call this via an Animation Event at the very end of the shoot animation.
    /// </summary>
    public void OnRangedAttackEnd()
    {
        OnAttackEnd();
    }
}
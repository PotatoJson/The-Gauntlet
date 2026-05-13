using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class CasterEnemy : BaseEnemy
{
    [Header("Caster Settings - Spells")]
    [SerializeField] private GameObject fireballWarningPrefab;
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private float fireballSpawnRadius = 5f;
    [SerializeField] private int fireballsPerTick = 3;
    [SerializeField] private float castInterval = 0.5f;
    [SerializeField] private float impactDelay = 1.5f;
    [SerializeField] private float fireballSpawnHeight = 15f; // How high up they spawn
    [SerializeField] private float fireballSpeed = 8f;

    [Header("Caster Settings - Movement")]
    [SerializeField] private float repositionTolerance = 0.5f;

    [Header("Caster Settings - Buffing")]
    [SerializeField] private float buffRadius = 10f;
    [SerializeField] private int maxBuffTargets = 2;
    [SerializeField] private float buffDamageMultiplier = 1.25f;
    [SerializeField] private float buffAttackSpeedMultiplier = 1.2f;
    [SerializeField] private GameObject buffVfxPrefab;
    [SerializeField] private float buffInterval = 10f;

    [Header("Caster Settings - Targeting")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeHeight = 10f;
    [SerializeField] private float groundProbeDistance = 30f;
    [SerializeField] private float warningSurfaceOffset = 0.02f;

    // State flags
    private bool isChanneling = false;
    private bool isRepositioning = false;
    private float castTimer = 0f;
    private float buffTimer;
    private Vector3 repositionTarget;

    // Animation parameter hash
    private static readonly int AnimDash = Animator.StringToHash("Dash");

    protected override void Update()
    {
        base.Update();

        if (isEngaged && !IsDead() && !isStunned && !isInHitStun && buffInterval > 0f)
        {
            buffTimer -= Time.deltaTime;
            if (buffTimer <= 0f)
            {
                BuffNearbyEnemies();
                buffTimer = buffInterval;
            }
        }

        // Handle the continuous casting intervals if actively channeling
        if (isChanneling && !isStunned && !isInHitStun && !IsDead())
        {
            castTimer -= Time.deltaTime;
            if (castTimer <= 0f)
            {
                CastFireballWave();
                castTimer = castInterval;
            }
        }

        if (isChanneling)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            FacePlayer();
        }
    }

    // Override engagement to completely skip the base "Opener Charge" logic
    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= engagementRange)
        {
            bool justEngaged = !isEngaged;
            isEngaged = true;
            hasOpenedWithCharge = true; // Act like it's already done so it doesn't try to charge

            if (justEngaged)
            {
                buffTimer = 0f;
            }
        }
        else if (isAware && !isAttacking)
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    // Disable charge attack completely for this enemy
    public override void ChargeAttack()
    {
        // Do nothing. This enemy only channels.
    }

    protected override void ContinueCombat()
    {
        // Don't act if we are already doing a heavy action
        if (isAttacking || isStunned || isInHitStun) return;

        if (isRepositioning)
        {
            UpdateRepositioning();
            return;
        }

        if (isChanneling)
        {
            navAgent.isStopped = true;
            FacePlayer();
            return;
        }

        float distance = GetDistanceToPlayer();

        if (IsOutsideAttackRange(distance))
        {
            BeginRepositioning();
            return;
        }

        StartChanneling();
    }

    public override void OnAttackEnd()
    {
        base.OnAttackEnd();

        if (IsDead()) return;

        StopChanneling();
        BeginRepositioning();
    }

    private void StartChanneling()
    {
        if (!CanPerformAction()) return;

        Debug.Log($"{gameObject.name}: Starting Channel!");
        isChanneling = true;
        isAttacking = true;
        castTimer = 0f; // Cast immediately on start

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();

        // Re-using the LightAttack trigger for the channeling animation as requested
        animator?.SetTrigger(AnimLightAttack);
    }

    private void StopChanneling()
    {
        isChanneling = false;
    }

    private void BeginRepositioning()
    {
        if (player == null || !navAgent.isOnNavMesh) return;

        Vector3 playerPosition = player.position;
        Vector3 directionFromPlayer = transform.position - playerPosition;
        directionFromPlayer.y = 0f;

        if (directionFromPlayer.sqrMagnitude < 0.001f)
        {
            directionFromPlayer = -transform.forward;
            directionFromPlayer.y = 0f;
        }

        Vector3 desiredPosition = playerPosition + directionFromPlayer.normalized * attackRange;

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, attackRange, NavMesh.AllAreas))
        {
            repositionTarget = hit.position;
        }
        else
        {
            repositionTarget = desiredPosition;
        }

        isRepositioning = true;
        navAgent.speed = chaseSpeed;
        navAgent.isStopped = false;
        navAgent.SetDestination(repositionTarget);
    }

    private void UpdateRepositioning()
    {
        if (navAgent.pathPending) return;

        if (navAgent.remainingDistance <= navAgent.stoppingDistance + repositionTolerance)
        {
            EndRepositioning();
            StartChanneling();
        }
    }

    private void EndRepositioning()
    {
        if (!isRepositioning) return;

        isRepositioning = false;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
    }

    private bool IsOutsideAttackRange(float distance)
    {
        return Mathf.Abs(distance - attackRange) > repositionTolerance;
    }

    private void BuffNearbyEnemies()
    {
        if (buffRadius <= 0f || maxBuffTargets <= 0)
        {
            Debug.LogWarning($"{gameObject.name}: Buff skipped (buffRadius={buffRadius}, maxBuffTargets={maxBuffTargets}).");
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, buffRadius);
        List<BaseEnemy> candidates = new List<BaseEnemy>();

        foreach (Collider hit in hits)
        {
            BaseEnemy enemy = hit.GetComponentInParent<BaseEnemy>();
            if (enemy == null)
            {
                Debug.Log($"{gameObject.name}: Buff scan ignored {hit.name} (no BaseEnemy).");
                continue;
            }

            if (enemy == this || enemy.IsDead() || enemy.IsBuffed())
            {
                Debug.Log($"{gameObject.name}: Buff scan ignored {enemy.gameObject.name} (self/dead/buffed).");
                continue;
            }

            if (!candidates.Contains(enemy)) candidates.Add(enemy);
        }

        Debug.Log($"{gameObject.name}: Buff candidates found = {candidates.Count}.");

        int maxTargets = Mathf.Min(maxBuffTargets, candidates.Count);
        if (maxTargets <= 0)
        {
            Debug.LogWarning($"{gameObject.name}: Buff aborted (no valid targets in range).");
            return;
        }

        int targetCount = Random.Range(1, maxTargets + 1);
        Debug.Log($"{gameObject.name}: Buffing {targetCount} enemies (maxTargets={maxTargets}).");

        for (int i = 0; i < candidates.Count; i++)
        {
            int swapIndex = Random.Range(i, candidates.Count);
            BaseEnemy temp = candidates[i];
            candidates[i] = candidates[swapIndex];
            candidates[swapIndex] = temp;
        }

        for (int i = 0; i < targetCount; i++)
        {
            Debug.Log($"{gameObject.name}: Applying buff to {candidates[i].gameObject.name}.");
            candidates[i].ApplyPermanentBuff(buffDamageMultiplier, buffAttackSpeedMultiplier, buffVfxPrefab);
        }
    }

    private void CastFireballWave()
    {
        if (player == null) return;

        for (int i = 0; i < fireballsPerTick; i++)
        {
            // Find a random point within a circle around the player
            Vector2 randomCirclePoint = Random.insideUnitCircle * fireballSpawnRadius;
            Vector3 basePosition = player.position + new Vector3(randomCirclePoint.x, 0f, randomCirclePoint.y);

            Vector3 targetPosition = basePosition;
            if (TryGetGroundPosition(basePosition, out Vector3 groundPosition))
            {
                targetPosition = groundPosition;
            }

            // Start the sequence to show warning -> wait -> spawn fireball
            StartCoroutine(FireballSequenceRoutine(targetPosition));
        }
    }

    private IEnumerator FireballSequenceRoutine(Vector3 targetPosition)
    {
        GameObject warningInstance = null;
        
        // 1. Spawn the projection/warning on the ground at targetPosition with X rotated by 90
        if (fireballWarningPrefab != null)
        {
            warningInstance = Instantiate(fireballWarningPrefab, targetPosition, Quaternion.Euler(90f, 0f, 0f));
        }

        // 2. Wait for the predetermined impact delay
        yield return new WaitForSeconds(impactDelay);

        GameObject fireballInstance = null;

        // 3. Spawn the actual fireball from the SKY shooting DOWNWARD
        if (fireballPrefab != null)
        {
            // Shift the start position HIGH up on the Y axis
            Vector3 skySpawnPosition = targetPosition + (Vector3.up * fireballSpawnHeight);
            
            // Point the fireball straight down at the ground
            Quaternion downwardRotation = Quaternion.LookRotation(Vector3.down);
            
            fireballInstance = Instantiate(fireballPrefab, skySpawnPosition, downwardRotation);
            fireballInstance.GetComponent<EnemyProjectile>()?.SetSpeed(fireballSpeed);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Fireball Prefab is missing!");
        }

        // 4. Wait until the fireball is destroyed before removing the warning
        if (fireballInstance != null)
        {
            while (fireballInstance != null)
            {
                yield return null; // Wait for the next frame
            }
        }

        // 5. Remove the projection
        if (warningInstance != null)
        {
            Destroy(warningInstance);
        }
    }

    private bool TryGetGroundPosition(Vector3 position, out Vector3 groundPosition)
    {
        Vector3 rayOrigin = position + Vector3.up * groundProbeHeight;
        float rayDistance = groundProbeHeight + groundProbeDistance;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundPosition = hit.point;
            groundPosition.y += warningSurfaceOffset;
            return true;
        }

        groundPosition = position;
        return false;
    }

    /// <summary>
    /// PUBLIC METHOD: Call this from the Throwable Object's script interaction on collision.
    /// </summary>
    public void HitByThrowable(float stunDuration)
    {
        Debug.Log($"{gameObject.name} was hit by a throwable! Stunned for {stunDuration} seconds.");

        StopChanneling();
        StopRepositioning();
        ApplyStun(stunDuration);
    }

    // --- Overrides to handle taking damage and dying while channeling/dashing ---

    public override void TakeDamage(float damage)
    {
        if (!isHitImmune)
        {
            StopChanneling();
            StopRepositioning();
        }

        base.TakeDamage(damage);
    }

    public override void ApplyStun(float duration)
    {
        StopChanneling();
        StopRepositioning();
        base.ApplyStun(duration);
    }

    protected override void Die()
    {
        StopChanneling();
        StopRepositioning();
        base.Die();
    }

    private void StopRepositioning()
    {
        isRepositioning = false;

        if (navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }
    }

    protected override bool CanPerformAction()
    {
        return base.CanPerformAction() && !isChanneling && !isRepositioning;
    }
}
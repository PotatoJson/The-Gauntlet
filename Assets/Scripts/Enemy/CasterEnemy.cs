using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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

    [Header("Caster Settings - Movement")]
    [SerializeField] private float evadeDistance = 5f;
    [SerializeField] private float dashDistance = 8f;
    [SerializeField] private float dashSpeed = 15f;

    // State flags
    private bool isChanneling = false;
    private bool isDashing = false;
    private float castTimer = 0f;

    // Animation parameter hash
    private static readonly int AnimDash = Animator.StringToHash("Dash");

    protected override void Update()
    {
        base.Update();

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
    }

    // Override engagement to completely skip the base "Opener Charge" logic
    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= engagementRange)
        {
            isEngaged = true;
            hasOpenedWithCharge = true; // Act like it's already done so it doesn't try to charge
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
        if (isAttacking || isStunned || isInHitStun || isDashing) return;

        float distance = GetDistanceToPlayer();

        // 1. Evade if player gets too close
        if (distance <= evadeDistance)
        {
            StartDash();
            return;
        }

        // 2. Channel spells if outside evade distance
        if (!isChanneling)
        {
            StartChanneling();
        }
        else
        {
            // Stop moving while channeling and face the player
            navAgent.isStopped = true;
            FacePlayer();
        }
    }

    private void StartChanneling()
    {
        Debug.Log($"{gameObject.name}: Starting Channel!");
        isChanneling = true;
        castTimer = 0f; // Cast immediately on start

        // Re-using the LightAttack trigger for the channeling animation as requested
        animator?.SetTrigger(AnimLightAttack);
    }

    private void StopChanneling()
    {
        isChanneling = false;
    }

    private void CastFireballWave()
    {
        if (player == null) return;

        for (int i = 0; i < fireballsPerTick; i++)
        {
            // Find a random point within a circle around the player
            Vector2 randomCirclePoint = Random.insideUnitCircle * fireballSpawnRadius;
            Vector3 targetPosition = player.position + new Vector3(randomCirclePoint.x, 0f, randomCirclePoint.y);

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

    private void StartDash()
    {
        Debug.Log($"{gameObject.name}: Player too close, Dashing away!");

        StopChanneling();
        isDashing = true;

        // Trigger dash animation
        animator?.SetTrigger(AnimDash);

        // Find a destination directly away from the player
        Vector3 directionAway = (transform.position - player.position).normalized;
        Vector3 targetDashPos = transform.position + (directionAway * dashDistance);

        // Ensure the dash position is actually on the NavMesh
        if (NavMesh.SamplePosition(targetDashPos, out NavMeshHit hit, dashDistance, NavMesh.AllAreas))
        {
            navAgent.speed = dashSpeed;
            navAgent.isStopped = false;
            navAgent.SetDestination(hit.position);

            StartCoroutine(WaitUntilDashCompletes());
        }
        else
        {
            // Failsafe if we're backed into a corner and can't dash further
            isDashing = false;
        }
    }

    private IEnumerator WaitUntilDashCompletes()
    {
        // Wait until distance to destination is small, or we get stunned/die
        while (isDashing && !isStunned && !isInHitStun && !IsDead())
        {
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
            {
                break;
            }
            yield return null;
        }

        EndDash();
    }

    private void EndDash()
    {
        if (isDashing)
        {
            isDashing = false;
            navAgent.speed = chaseSpeed; // Reset agent speed back to normal
            navAgent.isStopped = true;
        }
    }

    /// <summary>
    /// PUBLIC METHOD: Call this from the Throwable Object's script interaction on collision.
    /// </summary>
    public void HitByThrowable(float stunDuration)
    {
        Debug.Log($"{gameObject.name} was hit by a throwable! Stunned for {stunDuration} seconds.");

        StopChanneling();
        EndDash();
        ApplyStun(stunDuration);
    }

    // --- Overrides to handle taking damage and dying while channeling/dashing ---

    public override void TakeDamage(float damage)
    {
        if (!isHitImmune)
        {
            StopChanneling();
            EndDash();
        }

        base.TakeDamage(damage);
    }

    public override void ApplyStun(float duration)
    {
        StopChanneling();
        EndDash();
        base.ApplyStun(duration);
    }

    protected override void Die()
    {
        StopChanneling();
        EndDash();
        base.Die();
    }

    protected override bool CanPerformAction()
    {
        return base.CanPerformAction() && !isChanneling && !isDashing;
    }
}
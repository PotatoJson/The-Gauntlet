using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class CasterEnemy : BaseEnemy
{
    // Ranged enemies keep their own spacing; they don't queue for the melee attack tokens.
    protected override bool UsesAttackTokens => false;


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

    [Header("Caster Settings - VFX")]
    [Tooltip("Glowing circle on the floor marking where fireballs rain down; follows the player while the caster channels.")]
    [SerializeField] private GroundFx aoeZonePrefab;
    [Tooltip("Summoning circle under the caster while it channels.")]
    [SerializeField] private GroundFx channelCirclePrefab;
    [Tooltip("Small marker on the floor where one fireball is about to land. Replaces the plain warning decal when set.")]
    [SerializeField] private FireballWarningFx fireballWarningFxPrefab;
    [Tooltip("Burst where each fireball lands.")]
    [SerializeField] private ImpactFx fireballImpactPrefab;
    [SerializeField] private float fireballImpactRadius = 1.6f;
    [Tooltip("Ring that sweeps out from the caster when it buffs its allies.")]
    [SerializeField] private ImpactFx buffPulsePrefab;

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
        BeginChannelFx();

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();

        // Re-using the LightAttack trigger for the channeling animation as requested
        animator?.SetTrigger(AnimLightAttack);
    }

    private void StopChanneling()
    {
        isChanneling = false;
        EndChannelFx();
    }

    private GroundFx _aoeZone, _channelCircle;
    private Vector3 _zoneCenter;

    private void BeginChannelFx()
    {
        // The danger zone is planted on the floor where the player stands when the channel starts and
        // stays there: the fireballs fall inside it, so the player has to leave it, not just keep moving.
        if (aoeZonePrefab != null && player != null && _aoeZone == null)
        {
            _aoeZone = Instantiate(aoeZonePrefab, player.position, Quaternion.identity);
            _aoeZone.Begin(fireballSpawnRadius);
            _zoneCenter = _aoeZone.transform.position;
        }

        if (channelCirclePrefab != null && _channelCircle == null)
        {
            _channelCircle = Instantiate(channelCirclePrefab, transform.position, Quaternion.identity);
            _channelCircle.Begin(1.8f, transform);
        }
    }

    private void EndChannelFx()
    {
        if (_aoeZone != null) _aoeZone.End();
        if (_channelCircle != null) _channelCircle.End();
        _aoeZone = null;
        _channelCircle = null;
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

        if (buffPulsePrefab != null)
        {
            ImpactFx pulse = Instantiate(buffPulsePrefab, transform.position + Vector3.up * 0.1f, Quaternion.identity);
            pulse.Play(buffRadius, new Color(1f, 0.35f, 0.3f, 1f));
        }
    }

    private void CastFireballWave()
    {
        if (player == null) return;

        // Fireballs fall inside the planted zone; without the zone effect they fall around the player as before.
        Vector3 center = _aoeZone != null ? _zoneCenter : player.position;

        for (int i = 0; i < fireballsPerTick; i++)
        {
            // Find a random point within a circle around the centre
            Vector2 randomCirclePoint = Random.insideUnitCircle * fireballSpawnRadius;
            Vector3 basePosition = center + new Vector3(randomCirclePoint.x, 0f, randomCirclePoint.y);

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
        FireballWarningFx warningFx = null;

        // 1. Spawn the projection/warning on the ground at targetPosition. The effect marker fills up until
        //    the fireball lands (delay + the time it takes to fall); the old decal is the fallback.
        if (fireballWarningFxPrefab != null)
        {
            warningFx = Instantiate(fireballWarningFxPrefab, targetPosition + Vector3.up * 0.04f, Quaternion.identity);
            warningFx.Begin(fireballImpactRadius, impactDelay + fireballSpawnHeight / Mathf.Max(0.1f, fireballSpeed));
        }
        else if (fireballWarningPrefab != null)
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
            EnemyProjectile projectile = fireballInstance.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.SetSpeed(fireballSpeed);
                projectile.spawnImpactOnHit = false; // this routine plays the landing burst itself
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Fireball Prefab is missing!");
        }

        // 4. Follow the fireball down. It either lands (burst on the ground) or hits the player first
        //    (burst where it was); then the warning goes away.
        if (fireballInstance != null)
        {
            Vector3 lastPosition = targetPosition;

            while (fireballInstance != null)
            {
                lastPosition = fireballInstance.transform.position;
                if (lastPosition.y <= targetPosition.y + 0.3f) break; // reached the ground
                yield return null; // Wait for the next frame
            }

            if (warningFx != null) warningFx.End();

            if (fireballImpactPrefab != null)
            {
                Vector3 at = fireballInstance != null ? targetPosition : lastPosition;
                ImpactFx impact = Instantiate(fireballImpactPrefab, at + Vector3.up * 0.05f, Quaternion.identity);
                impact.Play(fireballImpactRadius);
            }

            if (fireballInstance != null) Destroy(fireballInstance);
        }

        // 5. Remove the projection
        if (warningFx != null) warningFx.End();
        if (warningInstance != null)
        {
            Destroy(warningInstance);
        }
    }

    private bool TryGetGroundPosition(Vector3 position, out Vector3 groundPosition)
    {
        // Probe from just above the spot, not 10m up: indoors that start is above the ceiling, and the
        // "ground" it found was the roof, so the markers and bursts ended up out of sight overhead.
        float probeHeight = Mathf.Min(groundProbeHeight, 2.5f);
        Vector3 rayOrigin = position + Vector3.up * probeHeight;
        float rayDistance = probeHeight + groundProbeDistance;

        // The player and enemies standing there are not the floor.
        int count = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _groundHits, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        groundPosition = position;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _groundHits[i];
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            if (hit.collider.GetComponentInParent<BaseEnemy>() != null) continue;

            if (hit.distance < best)
            {
                best = hit.distance;
                groundPosition = hit.point;
                groundPosition.y += warningSurfaceOffset;
                found = true;
            }
        }

        return found;
    }

    private readonly RaycastHit[] _groundHits = new RaycastHit[16];

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
        if (!isHitImmune && !IsDotTick)
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
using System.Collections;
using UnityEngine;

public class HellLord : BaseEnemy
{
    [Header("Hell Lord - Melee")]
    [SerializeField] private float swordSwingDamage = 30f;
    [SerializeField] private float attackDistance = 3f;
    [SerializeField] private SwordHitbox swordHitbox;

    [Header("Hell Lord - Beam Attack")]
    [SerializeField] private GameObject beamPrefab;
    [SerializeField] private Transform beamSpawnPoint;
    [SerializeField] private float beamDuration = 2.0f;
    [SerializeField] private float beamTickInterval = 0.25f;
    [SerializeField] private float beamDamage = 12f;
    [SerializeField] private float beamRange = 20f;
    [SerializeField] private LayerMask beamHitMask = ~0;

    [Header("Hell Lord - Casting (Fireball Wave)")]
    [SerializeField] private GameObject fireballWarningPrefab;
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private float fireballSpawnRadius = 5f;
    [SerializeField] private int fireballsPerTick = 3;
    [SerializeField] private float castInterval = 0.5f;
    [SerializeField] private float impactDelay = 1.5f;
    [SerializeField] private float fireballSpawnHeight = 15f;
    [SerializeField] private float fireballSpeed = 8f;
    [SerializeField] private int castWavesPerSequence = 3;

    [Header("Hell Lord - Casting Targeting")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeHeight = 10f;
    [SerializeField] private float groundProbeDistance = 30f;
    [SerializeField] private float warningSurfaceOffset = 0.02f;

    [Header("Hell Lord - AI")]
    [SerializeField] private float aiDecisionInterval = 0.4f;

    private float aiDecisionTimer;
    private bool isCasting;
    private bool isBeamActive;
    private int nextCastChunk = 2;

    private Coroutine beamRoutine;
    private Coroutine castRoutine;
    private GameObject beamInstance;

    private static readonly int AnimMeleeAttack = Animator.StringToHash("MeleeAttack");
    private static readonly int AnimBeamAttack = Animator.StringToHash("Blast");
    private static readonly int AnimCastAttack = Animator.StringToHash("Cast");
    private static readonly int AnimLightRandom = Animator.StringToHash("LightRandom");

    protected override void Start()
    {
        base.Start();
        hasOpenedWithCharge = true;
    }

    protected override void Update()
    {
        base.Update();

        if (IsDead() || isStunned || isInHitStun) return;

        if (isEngaged)
        {
            CheckCastHealthThreshold();
        }

        if (isCasting || isBeamActive)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            FacePlayer();
            return;
        }

        if (!isEngaged || isAttacking) return;

        aiDecisionTimer -= Time.deltaTime;
        if (aiDecisionTimer <= 0f)
        {
            aiDecisionTimer = aiDecisionInterval + Random.Range(-0.1f, 0.1f);
            MakeDecision();
        }
    }

    protected override void ContinueCombat()
    {
        if (isAttacking || isCharging || isStunned || isInHitStun || isCasting || isBeamActive) return;

        float distance = GetDistanceToPlayer();

        if (distance > attackDistance)
        {
            ChasePlayer();
            FacePlayer();
        }
        else
        {
            navAgent.isStopped = true;
            FacePlayer();
        }
    }

    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= engagementRange)
        {
            isEngaged = true;
            hasOpenedWithCharge = true;
        }
        else if (isAware && !isAttacking)
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    public override void ChargeAttack()
    {
        // Boss does not use charge attacks.
    }

    private void MakeDecision()
    {
        if (!CanPerformAction() || player == null) return;

        float distance = GetDistanceToPlayer();
        if (distance > attackDistance) return;

        if (Random.value < 0.20f)
        {
            BeamAttack();
        }
        else
        {
            LightAttack();
        }
    }

    public override void LightAttack()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        float randomIndex = Random.Range(0, 3) / 2f;
        animator?.SetFloat(AnimLightRandom, randomIndex);
        animator?.SetTrigger(AnimMeleeAttack);
    }

    private void BeamAttack()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        animator?.SetTrigger(AnimBeamAttack);

        if (animator == null)
        {
            BeginBeamAttack();
        }
    }

    public void BeginBeamAttack()
    {
        if (isBeamActive) return;

        isBeamActive = true;

        if (beamRoutine != null)
        {
            StopCoroutine(beamRoutine);
        }

        beamRoutine = StartCoroutine(BeamAttackRoutine());
    }

    public void EndBeamAttack()
    {
        if (beamRoutine != null)
        {
            StopCoroutine(beamRoutine);
            beamRoutine = null;
        }

        CleanupBeam();
        OnAttackEnd();
    }

    private IEnumerator BeamAttackRoutine()
    {
        float elapsed = 0f;
        float tickTimer = 0f;

        if (beamPrefab != null && beamSpawnPoint != null)
        {
            beamInstance = Instantiate(beamPrefab, beamSpawnPoint.position, beamSpawnPoint.rotation, beamSpawnPoint);
        }

        while (elapsed < beamDuration)
        {
            elapsed += Time.deltaTime;
            tickTimer -= Time.deltaTime;

            UpdateBeamTransform();

            if (tickTimer <= 0f)
            {
                tickTimer = beamTickInterval;
                TryDamageBeamTarget();
            }

            yield return null;
        }

        CleanupBeam();
        OnAttackEnd();
    }

    private void UpdateBeamTransform()
    {
        if (beamSpawnPoint == null || player == null) return;

        Vector3 directionToPlayer = (player.position + Vector3.up * 1.2f) - beamSpawnPoint.position;
        if (directionToPlayer.sqrMagnitude < 0.0001f)
        {
            directionToPlayer = transform.forward;
        }

        Quaternion rotation = Quaternion.LookRotation(directionToPlayer.normalized);
        beamSpawnPoint.SetPositionAndRotation(beamSpawnPoint.position, rotation);

        if (beamInstance != null)
        {
            beamInstance.transform.SetPositionAndRotation(beamSpawnPoint.position, rotation);
        }
    }

    private void TryDamageBeamTarget()
    {
        if (beamSpawnPoint == null || player == null) return;

        Vector3 direction = (player.position + Vector3.up * 1.2f) - beamSpawnPoint.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        if (Physics.Raycast(beamSpawnPoint.position, direction.normalized, out RaycastHit hit, beamRange, beamHitMask, QueryTriggerInteraction.Ignore))
        {
            PlayerHealth playerHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(beamDamage, 0, gameObject);
            }
        }
    }

    private void CleanupBeam()
    {
        if (beamInstance != null)
        {
            Destroy(beamInstance);
            beamInstance = null;
        }

        isBeamActive = false;
    }

    private void CheckCastHealthThreshold()
    {
        if (nextCastChunk <= 0 || isCasting) return;

        float healthPercent = currentHealth / maxHealth;
        float threshold = nextCastChunk / 3f;

        if (healthPercent <= threshold)
        {
            nextCastChunk--;
            StartCastSequence();
        }
    }

    private void StartCastSequence()
    {
        if (!CanPerformAction()) return;

        isCasting = true;
        isAttacking = true;
        attackCooldownTimer = attackCooldown;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        animator?.SetTrigger(AnimCastAttack);

        if (animator == null)
        {
            BeginCastSequence();
        }
    }

    public void BeginCastSequence()
    {
        if (castRoutine != null)
        {
            StopCoroutine(castRoutine);
        }

        castRoutine = StartCoroutine(CastSequenceRoutine());
    }

    private IEnumerator CastSequenceRoutine()
    {
        int waves = Mathf.Max(1, castWavesPerSequence);

        for (int i = 0; i < waves; i++)
        {
            CastFireballWave();
            yield return new WaitForSeconds(castInterval);
        }

        isCasting = false;
        castRoutine = null;
        OnAttackEnd();
    }

    private void CastFireballWave()
    {
        if (player == null) return;

        for (int i = 0; i < fireballsPerTick; i++)
        {
            Vector2 randomCirclePoint = Random.insideUnitCircle * fireballSpawnRadius;
            Vector3 basePosition = player.position + new Vector3(randomCirclePoint.x, 0f, randomCirclePoint.y);

            Vector3 targetPosition = basePosition;
            if (TryGetGroundPosition(basePosition, out Vector3 groundPosition))
            {
                targetPosition = groundPosition;
            }

            StartCoroutine(FireballSequenceRoutine(targetPosition));
        }
    }

    private IEnumerator FireballSequenceRoutine(Vector3 targetPosition)
    {
        GameObject warningInstance = null;

        if (fireballWarningPrefab != null)
        {
            warningInstance = Instantiate(fireballWarningPrefab, targetPosition, Quaternion.Euler(90f, 0f, 0f));
        }

        yield return new WaitForSeconds(impactDelay);

        GameObject fireballInstance = null;

        if (fireballPrefab != null)
        {
            Vector3 skySpawnPosition = targetPosition + (Vector3.up * fireballSpawnHeight);
            Quaternion downwardRotation = Quaternion.LookRotation(Vector3.down);

            fireballInstance = Instantiate(fireballPrefab, skySpawnPosition, downwardRotation);
            fireballInstance.GetComponent<EnemyProjectile>()?.SetSpeed(fireballSpeed);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Fireball Prefab is missing!");
        }

        if (fireballInstance != null)
        {
            while (fireballInstance != null)
            {
                yield return null;
            }
        }

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

    public override void TakeDamage(float damage)
    {
        if (!isHitImmune)
        {
            StopCasting();
            EndBeamAttack();
        }

        base.TakeDamage(damage);
    }

    public override void ApplyStun(float duration)
    {
        StopCasting();
        EndBeamAttack();
        base.ApplyStun(duration);
    }

    protected override void Die()
    {
        StopCasting();
        EndBeamAttack();
        base.Die();
    }

    private void StopCasting()
    {
        if (!isCasting) return;

        if (castRoutine != null)
        {
            StopCoroutine(castRoutine);
            castRoutine = null;
        }

        isCasting = false;
    }

    protected override bool CanPerformAction()
    {
        return base.CanPerformAction() && !isCasting && !isBeamActive;
    }

    #region Animation Events - Sword Hitbox
    public void EnableSwordSwingHitbox()
    {
        swordHitbox?.EnableHitbox(swordSwingDamage);
    }

    public void DisableSwordHitbox()
    {
        swordHitbox?.DisableHitbox();
    }
    #endregion

    #region Animation Events - Attack End
    public void OnSwordSwingEnd()
    {
        DisableSwordHitbox();
        OnAttackEnd();
    }
    #endregion
}
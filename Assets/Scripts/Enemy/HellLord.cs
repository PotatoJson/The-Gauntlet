using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

public class HellLord : BaseEnemy
{
    // Bosses fight on their own terms: no attack tokens, and no one else's alarm wakes them.
    protected override bool UsesAttackTokens => false;
    protected override bool CanBeAlerted => false;


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
    [SerializeField] private float castDuration = 10f;
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
    public LocalizedString bossNameString;
    [SerializeField] private float aiDecisionInterval = 0.4f;
    [SerializeField, Range(0f, 1f)] private float beamAttackChance = 0.10f;

    [Header("Hell Lord - Rotation")]
    [SerializeField] private float facePlayerTurnSpeed = 4f;

    [Header("Hell Lord - Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private float debugLogInterval = 1f;

    private float aiDecisionTimer;
    private bool isCasting;
    private bool isBeamActive;
    private int nextCastChunk = 2;
    private int lastMeleeIndex = -1;

    private Coroutine beamRoutine;
    private Coroutine castRoutine;
    private GameObject beamInstance;

    private Vector3 beamDirection;
    private Quaternion beamRotation;

    private static readonly int AnimMeleeAttack = Animator.StringToHash("MeleeAttack");
    private static readonly int AnimBeamAttack = Animator.StringToHash("Blast");
    private static readonly int AnimCastAttack = Animator.StringToHash("Cast");

    private bool skipBeamUntilMelee;
    private float debugLogTimer;

    protected override void Start()
    {
        base.Start();
        hasOpenedWithCharge = true;

        // A slow hellfire ring around his feet from the start, so he has presence before the fight begins.
        EnemyFx.Ring(transform, 3.5f, EnemyFx.Hell, 0.05f, 15f);
    }

    private GroundFx _castRing;
    private GroundFx _beamRing;

    private void EndCastFx()
    {
        if (_castRing != null) _castRing.End();
        _castRing = null;
    }

    protected override void Update()
    {
        base.Update();

        if (enableDebugLogs)
        {
            debugLogTimer -= Time.deltaTime;
            if (debugLogTimer <= 0f)
            {
                float distance = GetDistanceToPlayer();
                Debug.Log($"[HellLord DEBUG] {gameObject.name} state: aware={isAware}, engaged={isEngaged}, attacking={isAttacking}, casting={isCasting}, beam={isBeamActive}, stunned={isStunned}, hitStun={isInHitStun}, cooldown={attackCooldownTimer:F2}, distance={distance:F2}, nextCastChunk={nextCastChunk}");
                debugLogTimer = Mathf.Max(0.1f, debugLogInterval);
            }
        }

        if (isAttacking || isCasting || isBeamActive)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

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
            if (!isEngaged && BossHealthBar.Instance != null)
            {
                BossHealthBar.Instance.ShowBossHealthBar(this, currentHealth, maxHealth, bossNameString.GetLocalizedString());
            }
            
            if (enableDebugLogs && !isEngaged)
            {
                Debug.Log($"[HellLord DEBUG] {gameObject.name} engaged at distance {distance:F2}.");
            }

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
        if (!CanPerformAction() || player == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[HellLord DEBUG] MakeDecision blocked. CanPerformAction={CanPerformAction()}, playerNull={player == null}.");
            }
            return;
        }

        float distance = GetDistanceToPlayer();

        if (skipBeamUntilMelee)
        {
            if (distance > attackDistance)
            {
                ChasePlayer();
                FacePlayer();
                return;
            }

            LightAttack();
            return;
        }

        if (Random.value < beamAttackChance)
        {
            BeamAttack();
            return;
        }

        skipBeamUntilMelee = true;

        if (distance > attackDistance)
        {
            ChasePlayer();
            FacePlayer();
            return;
        }

        LightAttack();
    }

    public override void LightAttack()
    {
        if (!CanPerformAction())
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[HellLord DEBUG] LightAttack blocked. attacking={isAttacking}, casting={isCasting}, beam={isBeamActive}, stunned={isStunned}, hitStun={isInHitStun}, cooldown={attackCooldownTimer:F2}");
            }
            return;
        }

        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        skipBeamUntilMelee = false;

        int meleeIndex = GetNextMeleeIndex();
        float randomIndex = meleeIndex / 2f;
        animator?.SetFloat(AnimLightRandom, randomIndex);
        animator?.SetTrigger(AnimMeleeAttack);
    }

    private int GetNextMeleeIndex()
    {
        int nextIndex = Random.Range(0, 2);

        if (lastMeleeIndex < 0)
        {
            lastMeleeIndex = Random.Range(0, 3);
            return lastMeleeIndex;
        }

        if (nextIndex >= lastMeleeIndex)
        {
            nextIndex++;
        }

        lastMeleeIndex = nextIndex;
        return nextIndex;
    }

    private void BeamAttack()
    {
        if (!CanPerformAction())
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[HellLord DEBUG] BeamAttack blocked. attacking={isAttacking}, casting={isCasting}, beam={isBeamActive}, stunned={isStunned}, hitStun={isInHitStun}, cooldown={attackCooldownTimer:F2}");
            }
            return;
        }

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

        // Charging the beam: a tight, fast ring and a flare at his feet.
        EnemyFx.Impact(transform.position, 3f, EnemyFx.Fire);
        if (_beamRing != null) _beamRing.End();
        _beamRing = EnemyFx.Ring(transform, 2.6f, EnemyFx.Fire, 0.05f, 120f);

        if (beamSpawnPoint != null && player != null)
        {
            Vector3 targetPosition = player.position + Vector3.up * 1.2f;
            beamDirection = (targetPosition - beamSpawnPoint.position).normalized;
            if (beamDirection.sqrMagnitude < 0.0001f)
            {
                beamDirection = transform.forward;
            }

            beamRotation = Quaternion.LookRotation(beamDirection);
        }

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
        if (beamSpawnPoint == null) return;

        beamSpawnPoint.SetPositionAndRotation(beamSpawnPoint.position, beamRotation);

        if (beamInstance != null)
        {
            beamInstance.transform.SetPositionAndRotation(beamSpawnPoint.position, beamRotation);
        }
    }

    private void TryDamageBeamTarget()
    {
        if (beamSpawnPoint == null) return;

        if (Physics.Raycast(beamSpawnPoint.position, beamDirection, out RaycastHit hit, beamRange, beamHitMask, QueryTriggerInteraction.Ignore))
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

        if (_beamRing != null) _beamRing.End();
        _beamRing = null;

        isBeamActive = false;
    }

    private void CheckCastHealthThreshold()
    {
        if (nextCastChunk <= 0 || isCasting) return;

        float healthPercent = currentHealth / maxHealth;
        float threshold = nextCastChunk / 3f;

        if (healthPercent <= threshold)
        {
            if (!CanPerformAction())
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[HellLord DEBUG] Cast threshold met but blocked. health={healthPercent:F2}, threshold={threshold:F2}, attacking={isAttacking}, casting={isCasting}, beam={isBeamActive}, stunned={isStunned}, hitStun={isInHitStun}, cooldown={attackCooldownTimer:F2}");
                }
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[HellLord DEBUG] Cast threshold met. health={healthPercent:F2}, threshold={threshold:F2}. Starting cast.");
            }

            nextCastChunk--;
            StartCastSequence();
        }
    }

    private void StartCastSequence()
    {
        if (!CanPerformAction())
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[HellLord DEBUG] StartCastSequence blocked. attacking={isAttacking}, casting={isCasting}, beam={isBeamActive}, stunned={isStunned}, hitStun={isInHitStun}, cooldown={attackCooldownTimer:F2}");
            }
            return;
        }

        isCasting = true;
        isAttacking = true;
        attackCooldownTimer = attackCooldown;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        if (enableDebugLogs)
        {
            Debug.Log($"[HellLord DEBUG] StartCastSequence triggered. waves={castWavesPerSequence}, interval={castInterval:F2}");
        }

        animator?.SetTrigger(AnimCastAttack);

        BeginCastSequence();
    }

    public void BeginCastSequence()
    {
        if (castRoutine != null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[HellLord DEBUG] BeginCastSequence ignored (cast already running).");
            }
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log("[HellLord DEBUG] BeginCastSequence started.");
        }

        // A summoning circle as wide as the fireball field and a burst as it opens.
        EndCastFx();
        EnemyFx.Impact(transform.position, fireballSpawnRadius, EnemyFx.Hell);
        _castRing = EnemyFx.Ring(transform, fireballSpawnRadius, EnemyFx.Hell, 0.05f, 70f);

        castRoutine = StartCoroutine(CastSequenceRoutine());
    }

    private IEnumerator CastSequenceRoutine()
    {
        float duration = Mathf.Max(0.05f, castDuration);
        float elapsed = 0f;
        float timer = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                CastFireballWave();
                timer = castInterval;

                if (enableDebugLogs)
                {
                    Debug.Log($"[HellLord DEBUG] Casting wave at t={elapsed:F2}/{duration:F2}.");
                }
            }

            yield return null;
        }

        if (enableDebugLogs)
        {
            Debug.Log("[HellLord DEBUG] CastSequenceRoutine complete. Ending cast.");
        }

        isCasting = false;
        castRoutine = null;
        EndCastFx();
        OnAttackEnd();
    }

    private void CastFireballWave()
    {
        if (player == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[HellLord DEBUG] CastFireballWave aborted: player is null.");
            }
            return;
        }

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
        if (!isHitImmune && !IsDotTick)
        {
            StopCasting();
            EndBeamAttack();
        }

        // Unconditional: this boss could be damaged from range before ever engaging, and the old
        // '!isAware' guard meant that first hit was the ONLY chance to register the bar. Showing a
        // bar that already exists just refreshes it.
        if (BossHealthBar.Instance != null)
            BossHealthBar.Instance.ShowBossHealthBar(this, currentHealth, maxHealth, bossNameString.GetLocalizedString());

        base.TakeDamage(damage);

        if (BossHealthBar.Instance != null)
        {
            if (IsDead()) BossHealthBar.Instance.HideBossHealthBar(this);
            else BossHealthBar.Instance.UpdateHealth(this, currentHealth, maxHealth, bossNameString.GetLocalizedString());
        }
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

        // This boss never cleared its own bar, so it lingered until some other boss's death hid
        // the shared one.
        if (BossHealthBar.Instance != null) BossHealthBar.Instance.HideBossHealthBar(this);

        base.Die();
    }

    private void StopCasting()
    {
        EndCastFx();
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
        swordHitbox?.EnableHitbox(swordSwingDamage, heavyAttackPoiseDamage);

        // The visible arc of the blade.
        EnemyFx.Slash(transform, 3.4f, 0.9f, EnemyFx.Hell, 170f, 0.35f);
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
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
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
    [SerializeField, Tooltip("How long the player can stay out of sword reach before he fires the beam at them")]
    private float beamChaseSeconds = 3f;

    [Header("Hell Lord - Casting (Fireball Wave)")]
    [SerializeField] private FireballWarningFx fireballWarningFxPrefab;
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private float fireballImpactRadius = 1.8f;
    [SerializeField] private float fireballSpawnRadius = 5f;
    [SerializeField] private int fireballsPerTick = 3;
    [SerializeField] private float castInterval = 0.5f;
    [SerializeField] private float castDuration = 10f;
    [SerializeField] private float impactDelay = 1.5f;
    [SerializeField] private float fireballSpawnHeight = 15f;
    [SerializeField] private float fireballSpeed = 8f;
    [SerializeField] private int castWavesPerSequence = 3;
    [SerializeField, Tooltip("He stands still for this part of the cast, then fights on while the rest of the meteors fall")]
    private float castChannelSeconds = 3f;
    [SerializeField, Tooltip("Damage to a player standing in the marked circle when a meteor lands")]
    private float meteorLandingDamage = 15f;
    [SerializeField, Tooltip("One meteor in every this many waves aims at where the player is heading")]
    private int aimedWaveInterval = 2;

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
    private float _outOfReachSeconds;
    private int _waveCount;
    private Vector3 _lastWavePlayerPosition;

    private Coroutine beamRoutine;
    private Coroutine castRoutine;
    private GameObject beamInstance;

    public GameObject BeamPrefab => beamPrefab;
    public Transform BeamSpawnPoint => beamSpawnPoint;

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

        if (swordHitbox != null) _bladeTrail = EnemyFx.BladeTrail(swordHitbox.gameObject);
    }

    private const string AuraPrefabName = "HellAuraFx";
    private const float AuraFadeSeconds = 1.5f;

    private TrailRenderer _bladeTrail;
    private ParticleSystem _aura;
    private bool _swordSwinging;

    // Hellfire rises around him for as long as he is channelling a cast or the beam.
    private void BeginAura()
    {
        EndAura();
        _aura = EnemyFx.Fury(transform, AuraPrefabName);
    }

    private void EndAura()
    {
        if (_aura == null) return;

        _aura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(_aura.gameObject, AuraFadeSeconds);
        _aura = null;
    }

    protected override void Update()
    {
        base.Update();

        // A hit can cut the swing before its animation switches the hitbox off.
        if (_bladeTrail != null) _bladeTrail.emitting = _swordSwinging && isAttacking;

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

        // Running from him doesn't work: stay out of sword reach too long and he fires the beam.
        _outOfReachSeconds = distance > attackDistance ? _outOfReachSeconds + aiDecisionInterval : 0f;
        if (_outOfReachSeconds >= beamChaseSeconds && distance <= beamRange && HasLineOfSightToPlayer())
        {
            _outOfReachSeconds = 0f;
            BeamAttack();
            return;
        }

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

    protected override string AttackEvent => "event:/Boss/Boss_Attack";
    protected override string DeathEvent => "event:/Boss/Boss_Death";

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

        PlayAttackSound();

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
        if (!isAttacking) return; // a hit cancelled the attack during the wind-up, but its animation still played on

        isBeamActive = true;

        // Charging the beam: a flare at his feet and hellfire around him.
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/HellLord_Beam", transform.position);
        EnemyFx.Impact(transform.position, 3f, EnemyFx.Hell);
        BeginAura();

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
        if (!enabled) return; // his intro cutscene plays this same clip while he is still a statue

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

        if (isBeamActive) EndAura(); // a hit also lands here while he is casting, which keeps its own aura

        isBeamActive = false;
    }

    private void CheckCastHealthThreshold()
    {
        if (nextCastChunk <= 0 || castRoutine != null) return;

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

        // A burst as the summoning starts, then hellfire around him until it ends.
        EnemyFx.Impact(transform.position, fireballSpawnRadius, EnemyFx.Hell);
        BeginAura();

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/HellLord_Cast", transform.position);
        _waveCount = 0;
        if (player != null) _lastWavePlayerPosition = player.position;
        castRoutine = StartCoroutine(CastSequenceRoutine());
    }

    private void EndCastChannel()
    {
        if (!isCasting) return;

        isCasting = false;
        EndAura();
        OnAttackEnd();
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

            if (elapsed >= castChannelSeconds) EndCastChannel();

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

        castRoutine = null;
        EndCastChannel();
    }

    private float MeteorFlightSeconds => impactDelay + fireballSpawnHeight / Mathf.Max(0.1f, fireballSpeed);

    // Where the player will be when a meteor lands if they keep going, stopping at the edge of the floor.
    private Vector3 AheadOfPlayer(Vector3 playerVelocity)
    {
        Vector3 ahead = player.position + playerVelocity * MeteorFlightSeconds;

        if (NavMesh.SamplePosition(player.position, out NavMeshHit start, 2f, NavMesh.AllAreas)
            && NavMesh.Raycast(start.position, ahead, out NavMeshHit edge, NavMesh.AllAreas))
        {
            return edge.position;
        }

        return ahead;
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

        Vector3 playerVelocity = (player.position - _lastWavePlayerPosition) / Mathf.Max(0.05f, castInterval);
        _lastWavePlayerPosition = player.position;
        _waveCount++;

        for (int i = 0; i < fireballsPerTick; i++)
        {
            Vector2 randomCirclePoint = Random.insideUnitCircle * fireballSpawnRadius;
            Vector3 basePosition = player.position + new Vector3(randomCirclePoint.x, 0f, randomCirclePoint.y);

            if (i == 0 && _waveCount % aimedWaveInterval == 0) basePosition = AheadOfPlayer(playerVelocity);

            StartCoroutine(FireballSequenceRoutine(EnemyFx.FloorUnder(basePosition)));
        }
    }

    private IEnumerator FireballSequenceRoutine(Vector3 targetPosition)
    {
        // The sigil fills up until the moment the meteor lands: the delay plus the time it takes to fall.
        FireballWarningFx warning = null;

        if (fireballWarningFxPrefab != null)
        {
            warning = Instantiate(fireballWarningFxPrefab, targetPosition + Vector3.up * 0.04f, Quaternion.identity);
            warning.Begin(fireballImpactRadius, MeteorFlightSeconds);
        }

        yield return new WaitForSeconds(impactDelay);

        GameObject fireballInstance = null;

        if (fireballPrefab != null)
        {
            Vector3 skySpawnPosition = targetPosition + (Vector3.up * fireballSpawnHeight);
            Quaternion downwardRotation = Quaternion.LookRotation(Vector3.down);

            fireballInstance = Instantiate(fireballPrefab, skySpawnPosition, downwardRotation);
            EnemyProjectile projectile = fireballInstance.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.SetSpeed(fireballSpeed);
                projectile.spawnImpactOnHit = false; // the landing below plays the burst itself
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Fireball Prefab is missing!");
        }

        // It either reaches the ground, which cracks, or hits the player on the way down.
        Vector3 impactPoint = targetPosition;
        bool reachedGround = false;

        while (fireballInstance != null)
        {
            impactPoint = fireballInstance.transform.position;
            if (impactPoint.y <= targetPosition.y + 0.3f)
            {
                reachedGround = true;
                impactPoint = targetPosition;
                Destroy(fireballInstance);
                break;
            }

            yield return null;
        }

        if (warning != null) warning.End();

        if (fireballPrefab != null)
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/Enemies/Fireball_Explode", impactPoint);
            EnemyFx.Impact(impactPoint, fireballImpactRadius * 1.4f, EnemyFx.Hell);
            if (reachedGround)
            {
                EnemyFx.GroundCrack(impactPoint, fireballImpactRadius, EnemyFx.Hell);

                if (player != null && playerHealth != null && Vector3.Distance(player.position, impactPoint) <= fireballImpactRadius)
                {
                    playerHealth.TakeDamage(meteorLandingDamage, 0, gameObject);
                }
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        // A hit breaks the beam, but not a meteor cast: those only come twice a fight.
        if (!isHitImmune && !IsDotTick)
        {
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
        SteamAchievements.Unlock(SteamAchievements.BossHellLord);
        StoryDialogue.Say("Story_A2_Win_01", Speaker.Pergius);
        StoryDialogue.Say("Story_A2_Win_02", Speaker.Pergius);
        StopCasting();
        EndBeamAttack();

        // This boss never cleared its own bar, so it lingered until some other boss's death hid
        // the shared one.
        if (BossHealthBar.Instance != null) BossHealthBar.Instance.HideBossHealthBar(this);

        base.Die();
    }

    private void StopCasting()
    {
        if (castRoutine == null) return;

        StopCoroutine(castRoutine);
        castRoutine = null;

        if (isCasting) nextCastChunk++; // cut short while channelling, so he gets to cast it again once he recovers

        EndAura();
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
        _swordSwinging = true; // the blade leaves a trail for as long as it can hurt
    }

    public void DisableSwordHitbox()
    {
        swordHitbox?.DisableHitbox();
        _swordSwinging = false;
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
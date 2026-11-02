using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public class DemonBoss : BaseEnemy
{
    // Bosses fight on their own terms: no attack tokens, and no one else's alarm wakes them.
    protected override bool UsesAttackTokens => false;
    protected override bool CanBeAlerted => false;


    [Header("Boss Settings")]
    public LocalizedString bossNameString;
    [SerializeField, Tooltip("Health threshold (0 to 1) to trigger Phase 2")]
    private float phaseTwoThreshold = 0.5f;
    [SerializeField, Tooltip("Chance to dodge each time the player starts a swing in melee range (0.0 to 1.0)")]
    private float dodgeChance = 0.25f;
    [SerializeField, Tooltip("How far back the dodge carries the boss")]
    private float dodgeDistance = 4f;
    [SerializeField, Tooltip("How long the dodge lasts. Only damage over time hurts him during it")]
    private float dodgeSeconds = 0.4f;
    [SerializeField, Tooltip("The shortest time between two dodges")]
    private float dodgeCooldown = 4f;
    [SerializeField, Tooltip("How long the intermission animation lasts before forcing Phase 2")]
    private float intermissionDuration = 5.8f;
    [SerializeField, Tooltip("Chase speed is multiplied by this in Phase 2")]
    private float phaseTwoSpeedMultiplier = 1.3f;
    [SerializeField, Tooltip("The time between attacks is multiplied by this in Phase 2")]
    private float phaseTwoCooldownMultiplier = 0.67f;

    [Header("Charge")]
    [SerializeField, Tooltip("How long the player can stay out of reach before the boss charges them")]
    private float chargeAfterSeconds = 3f;
    [SerializeField, Tooltip("The boss only charges a player at least this far away")]
    private float chargeMinDistance = 6f;
    [SerializeField, Tooltip("The boss gives up a charge after this long")]
    private float chargeMaxSeconds = 2.5f;

    [Header("Phase 2 Weapons")]
    [SerializeField, Tooltip("The sword attached to the boss's left hand")]
    private GameObject leftSword;
    [SerializeField, Tooltip("The sword attached to the boss's right hand")]
    private GameObject rightSword;

    [Header("Rotation")]
    [SerializeField, Tooltip("How quickly the boss rotates toward the player")]
    private float facePlayerTurnSpeed = 4f;

    // Boss States
    private bool isPhase2 = false;
    private bool isIntermission = false;
    private bool isDodging = false;

    // Animator Hashes for Performance
    private static readonly int AnimMeleeAttack = Animator.StringToHash("MeleeAttack");
    private static readonly int AnimLightRand = Animator.StringToHash("LightRandom");

    private static readonly int AnimIntermission = Animator.StringToHash("Intermission");

    private static readonly int AnimDualWieldAttack = Animator.StringToHash("DualWieldAttack");
    private static readonly int AnimDualRand = Animator.StringToHash("DualRand");

    private static readonly int AnimDodgeBackwards = Animator.StringToHash("DodgeBack");

    private Coroutine _attackReset;
    private float _outOfReachSeconds;
    private float _chargeSeconds;
    private bool _playerWasSwinging;
    private float _nextDodgeTime;

    protected override void Awake()
    {
        base.Awake();

        // Ensure Phase 2 swords are hidden during Phase 1. Not in Start: that waits for his intro cutscene.
        if (leftSword != null) leftSword.SetActive(false);
        if (rightSword != null) rightSword.SetActive(false);
    }

    protected override void Start()
    {
        base.Start();
        // Immediately mark this true so the boss skips the opener charge sequence from BaseEnemy
        hasOpenedWithCharge = true;
    }

    protected override string AttackEvent => "event:/Boss/Boss_Attack";
    protected override string DeathEvent => "event:/Boss/Boss_Death";

    // A fist or blade this close to the floor on its hit frame has struck the ground.
    private const float FistGroundHeight = 1.5f;
    private const float BladeGroundHeight = 0.5f;
    private const float BladeGroundCheckSeconds = 0.12f;
    private const float StrikeMargin = 0.5f; // how far past the fist or blade tip a blow still connects
    private const float AttackResetSeconds = 3.5f; // longer than the longest attack animation

    private static readonly HumanBodyBones[] StrikingLimbs =
    {
        HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
    };

    private readonly List<TrailRenderer> _bladeTrails = new List<TrailRenderer>();
    private ParticleSystem _fury;

    // The hit frame of a blow: sparks where it lands in the air, or a shockwave and cracks where it hits the ground.
    protected override void OnMeleeHitFx(bool heavy)
    {
        if (heavy) FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", transform.position);

        if (isPhase2) StartCoroutine(PlaySwordHitFx());
        else PlayUnarmedHitFx();
    }

    // The hitbox runs from the boss out to the weapon, so a blow connects as far as it visibly reaches.
    protected override void DealHeavyHit()
    {
        Transform strike = isPhase2 ? ForemostBladeTip() : ForemostLimb();
        if (strike == null)
        {
            base.DealHeavyHit();
            return;
        }

        float defaultReach = heavyAttackHitboxOffset.z + heavyAttackHitboxSize.z * 0.5f;
        float reach = Mathf.Max(defaultReach, Forwardness(strike) + StrikeMargin);

        Vector3 offset = new Vector3(heavyAttackHitboxOffset.x, heavyAttackHitboxOffset.y, reach * 0.5f);
        Vector3 size = new Vector3(heavyAttackHitboxSize.x, heavyAttackHitboxSize.y, reach);
        TryDamagePlayerHitbox(heavyAttackDamage, heavyAttackPoiseDamage, offset, size);
    }

    // Whichever fist or foot is furthest forward is the one that was thrown.
    private Transform ForemostLimb()
    {
        Transform limb = null;
        foreach (HumanBodyBones bone in StrikingLimbs)
        {
            Transform candidate = animator != null ? animator.GetBoneTransform(bone) : null;
            if (candidate != null && (limb == null || Forwardness(candidate) > Forwardness(limb))) limb = candidate;
        }
        return limb;
    }

    private Transform ForemostBladeTip()
    {
        Transform foremost = null;
        foreach (TrailRenderer trail in _bladeTrails)
        {
            if (foremost == null || Forwardness(trail.transform) > Forwardness(foremost)) foremost = trail.transform;
        }
        return foremost;
    }

    private void PlayUnarmedHitFx()
    {
        Transform limb = ForemostLimb();
        if (limb == null) return;

        if (HeightAboveFeet(limb) < FistGroundHeight) PlayGroundHitFx(limb.position, 2.6f, EnemyFx.Fire);
        else EnemyFx.Strike(limb.position, 1.6f, EnemyFx.Fire);
    }

    // The blades are still dropping fast on the hit frame, so follow them for a moment to see if they reach the floor.
    private IEnumerator PlaySwordHitFx()
    {
        for (float elapsed = 0f; elapsed < BladeGroundCheckSeconds; elapsed += Time.deltaTime)
        {
            foreach (TrailRenderer trail in _bladeTrails)
            {
                if (HeightAboveFeet(trail.transform) < BladeGroundHeight)
                {
                    PlayGroundHitFx(trail.transform.position, 3.2f, EnemyFx.Hell);
                    yield break;
                }
            }
            yield return null;
        }

        Transform foremost = ForemostBladeTip();
        if (foremost != null) EnemyFx.Strike(foremost.position, 1.6f, EnemyFx.Hell);
    }

    private static void PlayGroundHitFx(Vector3 point, float radius, Color tint)
    {
        EnemyFx.Impact(point, radius, tint);
        EnemyFx.GroundCrack(point, radius);
    }

    private float Forwardness(Transform point) => Vector3.Dot(point.position - transform.position, transform.forward);
    private float HeightAboveFeet(Transform point) => point.position.y - transform.position.y;

    protected override void Update()
    {
        // Intercept Update completely during intermission so BaseEnemy cannot force movement/rotation
        if (isIntermission)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            UpdateAnimatorParameters(); // Keep animation speeds zeroed
            return;
        }

        base.Update();

        foreach (TrailRenderer trail in _bladeTrails) trail.emitting = isAttacking;
    }

    protected override void ContinueCombat()
    {
        bool playerSwinging = playerManager != null && playerManager.GetCurrentState() == PlayerState.Attacking;
        bool swingStarted = playerSwinging && !_playerWasSwinging;
        _playerWasSwinging = playerSwinging;

        if (isAttacking || isCharging || isStunned || isInHitStun || isDodging) return;

        float distance = GetDistanceToPlayer();

        if (swingStarted && distance <= attackRange && Time.time >= _nextDodgeTime && Random.value <= dodgeChance)
        {
            StartCoroutine(DodgeRoutine());
            return;
        }

        // If distance is less than attack range, try to attack
        if (distance <= attackRange)
        {
            _outOfReachSeconds = 0f;
            FacePlayer();
            LightAttack();
        }
        else
        {
            _outOfReachSeconds += Time.deltaTime;
            if (_outOfReachSeconds >= chargeAfterSeconds && distance >= chargeMinDistance)
            {
                ChargeAttack();
                if (isCharging) return;
            }

            ChasePlayer();
            FacePlayer();
        }
    }

    public override void TakeDamage(float damage)
    {
        if (IsDead() || isIntermission) return; // nothing interrupts or skips the roar
        if (isDodging && !IsDotTick) return;

        // Wake up boss if ambiently hit
        if (!isAware)
        {
            isAware = true;
            navAgent.isStopped = false;

            // NEW: Show health bar if hit from afar before aggroing!
            if (BossHealthBar.Instance != null)
            {
                BossHealthBar.Instance.ShowBossHealthBar(this, currentHealth, maxHealth, bossNameString.GetLocalizedString());
            }

        }

        isEngaged = true;
        hasOpenedWithCharge = true;

        currentHealth -= damage;

        // Passing 'this' keeps each boss on its own bar, so two bosses can be fought at once.
        if (BossHealthBar.Instance != null)
        {
            BossHealthBar.Instance.UpdateHealth(this, currentHealth, maxHealth, bossNameString.GetLocalizedString());
        }

        if (currentHealth <= 0)
        {
            if (BossHealthBar.Instance != null) BossHealthBar.Instance.HideBossHealthBar(this);
            Die();
            return;
        }

        // Check for Phase 2 Transition (50% health)
        float healthRatio = currentHealth / maxHealth;
        if (!isPhase2 && !isIntermission && healthRatio <= phaseTwoThreshold)
        {
            Debug.Log($"[DemonBoss] Health at {healthRatio * 100}% - Triggering Phase 2 Intermission!");
            StartIntermission();
            return;
        }
    }

    protected override void EnterHitStun()
    {
        // Do nothing. Bosses don't get staggered.
    }

    protected override void CheckEngagement()
    {
        if (player == null || isIntermission) return;

        float distance = GetDistanceToPlayer();
        if (distance <= engagementRange)
        {
            // NEW: Show the boss health bar the moment the boss aggros!
            if (!isEngaged && BossHealthBar.Instance != null)
            {
                BossHealthBar.Instance.ShowBossHealthBar(this, currentHealth, maxHealth, bossNameString.GetLocalizedString());
            }

            isEngaged = true;
            hasOpenedWithCharge = true; // Always true for the boss
        }
        else if (isAware && !isAttacking)
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    // He has no charge animation of his own: he sprints at the player, then swings as normal on arrival.
    public override void ChargeAttack()
    {
        if (!CanPerformAction() || player == null) return;

        base.ChargeAttack();
        _outOfReachSeconds = 0f;
        _chargeSeconds = 0f;
        PlayAttackSound();
        EnemyFx.Impact(transform.position, 3f, isPhase2 ? EnemyFx.Hell : EnemyFx.Fire);
    }

    protected override void UpdateChargeAttack()
    {
        _chargeSeconds += Time.deltaTime;

        if (player == null || _chargeSeconds >= chargeMaxSeconds)
        {
            EndCharge();
            return;
        }

        if (GetDistanceToPlayer() <= attackRange)
        {
            EndCharge();
            attackCooldownTimer = 0f;
            FacePlayerImmediate();
            BeginAttack();
            return;
        }

        navAgent.speed = chargeSpeed;
        navAgent.SetDestination(player.position);
    }

    public override void HeavyAttack() { }

    public override void LightAttack()
    {
        if (!CanPerformAction()) return;

        BeginAttack();
    }

    private void BeginAttack()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        PlayAttackSound();

        if (!isPhase2)
        {
            float randomIndex = Random.Range(0, 3) / 2f;
            animator?.SetFloat(AnimLightRand, randomIndex);
            animator?.SetTrigger(AnimMeleeAttack);
        }
        else
        {
            float dualRandomIndex = Random.Range(0, 4) / 3f;
            animator?.SetFloat(AnimDualRand, dualRandomIndex);
            animator?.SetTrigger(AnimDualWieldAttack);
        }

        // Safety reset to prevent the boss from getting permanently stuck if an animation event is missed
        if (_attackReset != null) StopCoroutine(_attackReset); // the last attack's must not cut this one short
        _attackReset = StartCoroutine(AttackResetFailsafe(AttackResetSeconds));
    }

    protected override void Die()
    {
        SteamAchievements.Unlock(SteamAchievements.BossDemon);

        foreach (TrailRenderer trail in _bladeTrails) trail.emitting = false;
        _bladeTrails.Clear();
        if (_fury != null) _fury.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        base.Die();
    }

    private void StartIntermission()
    {
        isIntermission = true;
        isPhase2 = true;

        chaseSpeed *= phaseTwoSpeedMultiplier;
        attackCooldown *= phaseTwoCooldownMultiplier;
        EndCharge();

        if (leftSword != null) leftSword.SetActive(true);
        if (rightSword != null) rightSword.SetActive(true);

        isAttacking = false;
        isDodging = false;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        animator?.SetTrigger(AnimIntermission);

        // Phase 2 begins: a blast, then he burns with fury and his blades leave trails for the rest of the fight.
        EnemyFx.Impact(transform.position, 7f, EnemyFx.Rage);
        _fury = EnemyFx.Fury(transform);

        foreach (GameObject sword in new[] { leftSword, rightSword })
        {
            TrailRenderer trail = EnemyFx.BladeTrail(sword);
            if (trail != null) _bladeTrails.Add(trail);
        }

        StartCoroutine(IntermissionTimer());
    }

    private IEnumerator IntermissionTimer()
    {
        yield return new WaitForSeconds(intermissionDuration);
        if (isIntermission)
        {
            OnIntermissionEnd();
        }
    }

    private IEnumerator AttackResetFailsafe(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        if (isAttacking)
        {
            Debug.LogError("[DemonBoss] Attack reset failsafe triggered: isAttacking was never set to false by OnAttackEnd().");
            isAttacking = false;
            if (player != null && !isStunned && !IsDead())
            {
                navAgent.isStopped = false;
            }
        }
    }

    // He hops back out of the player's swing. The clip's own root motion is too small to clear it, so the
    // nav agent carries him, which also stops him at walls and ledges.
    private IEnumerator DodgeRoutine()
    {
        isDodging = true;
        _nextDodgeTime = Time.time + dodgeCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator?.SetTrigger(AnimDodgeBackwards);

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        away.Normalize();

        for (float elapsed = 0f; elapsed < dodgeSeconds && isDodging; elapsed += Time.deltaTime)
        {
            navAgent.Move(away * (dodgeDistance / dodgeSeconds * Time.deltaTime));
            yield return null;
        }

        if (!isDodging) yield break; // the roar took over

        isDodging = false;
        if (!isStunned && !IsDead()) navAgent.isStopped = false;
    }

    public void OnIntermissionEnd()
    {
        isIntermission = false;
        isAttacking = false;
        attackCooldownTimer = 0f; // he comes out of the roar swinging
        if (player != null && !isStunned && !IsDead())
        {
            navAgent.isStopped = false;
        }
    }

    public void OnDodgeEnd() { } // the clip fires this, but DodgeRoutine times the dodge

    protected override bool CanPerformAction()
    {
        return base.CanPerformAction() && !isIntermission && !isDodging;
    }

    protected override float FacePlayerTurnSpeed => facePlayerTurnSpeed;
}
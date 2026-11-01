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
    [SerializeField, Tooltip("Chance to dodge when player is in melee range (0.0 to 1.0)")]
    private float dodgeChance = 0.10f;
    [SerializeField, Tooltip("How long the intermission animation lasts before forcing Phase 2")]
    private float intermissionDuration = 3.0f;

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

    // Debugging Throttle (prevents chat spam every single frame)
    private float debugTimer = 0f;

    protected override void Start()
    {
        base.Start();
        // Immediately mark this true so the boss skips the opener charge sequence from BaseEnemy
        hasOpenedWithCharge = true;

        // Ensure Phase 2 swords are hidden during Phase 1
        if (leftSword != null) leftSword.SetActive(false);
        if (rightSword != null) rightSword.SetActive(false);
    }

    protected override string AttackEvent => "event:/Boss/Boss_Attack";
    protected override string DeathEvent => "event:/Boss/Boss_Death";

    // A fist or blade this close to the floor on its hit frame has struck the ground.
    private const float FistGroundHeight = 1.5f;
    private const float BladeGroundHeight = 0.5f;
    private const float BladeGroundCheckSeconds = 0.12f;

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

    private void PlayUnarmedHitFx()
    {
        // Whichever fist or foot is furthest forward is the one that was thrown.
        Transform limb = null;
        foreach (HumanBodyBones bone in StrikingLimbs)
        {
            Transform candidate = animator != null ? animator.GetBoneTransform(bone) : null;
            if (candidate != null && (limb == null || Forwardness(candidate) > Forwardness(limb))) limb = candidate;
        }
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

        Transform foremost = null;
        foreach (TrailRenderer trail in _bladeTrails)
        {
            if (foremost == null || Forwardness(trail.transform) > Forwardness(foremost)) foremost = trail.transform;
        }
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

    // ============================================
    // DEBUG: OVERRIDE CONTINUE COMBAT TO TRACK AI
    // ============================================
    protected override void ContinueCombat()
    {
        if (isAttacking || isCharging || isStunned || isInHitStun)
        {
            if (Time.time > debugTimer)
            {
                Debug.Log($"[DemonBoss DEBUG] Combat Halted. isAttacking:{isAttacking}, isCharging:{isCharging}, isStunned:{isStunned}, isInHitStun:{isInHitStun}");
                debugTimer = Time.time + 1f; // Log once per second
            }
            return;
        }

        float distance = GetDistanceToPlayer();

        if (Time.time > debugTimer)
        {
            Debug.Log($"[DemonBoss DEBUG] Distance to player: {distance:F2} | Required Attack Range: {attackRange:F2}");
            debugTimer = Time.time + 1f;
        }

        // If distance is less than attack range, try to attack
        if (distance <= attackRange)
        {
            Debug.Log($"[DemonBoss DEBUG] In range! Attempting to FacePlayer and LightAttack...");
            FacePlayer();
            LightAttack();
        }
        else
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    public override void TakeDamage(float damage)
    {
        if (IsDead()) return;

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

    public override void ChargeAttack() { }
    public override void HeavyAttack() { }

    public override void LightAttack()
    {
        Debug.Log($"[DemonBoss DEBUG] LightAttack() triggered.");

        if (!CanPerformAction() || isIntermission || isDodging)
        {
            Debug.LogWarning($"[DemonBoss DEBUG] LightAttack Blocked! CanPerformAction(): {CanPerformAction()}, isIntermission: {isIntermission}, isDodging: {isDodging}");
            return;
        }

        if (Random.Range(0f, 1f) <= dodgeChance)
        {
            Debug.Log("[DemonBoss] Dodging instead of attacking.");
            PerformDodge();
            return;
        }

        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        PlayAttackSound();

        if (!isPhase2)
        {
            float randomIndex = Random.Range(0, 3) / 2f;
            Debug.Log($"[DemonBoss] Phase 1 Attack executing. Blend value: {randomIndex}");
            animator?.SetFloat(AnimLightRand, randomIndex);
            animator?.SetTrigger(AnimMeleeAttack);
        }
        else
        {
            float dualRandomIndex = Random.Range(0, 4) / 3f;
            Debug.Log($"[DemonBoss] Phase 2 Dual Wield Attack executing. Blend value: {dualRandomIndex}");
            animator?.SetFloat(AnimDualRand, dualRandomIndex);
            animator?.SetTrigger(AnimDualWieldAttack);
        }

        // Safety reset to prevent the boss from getting permanently stuck if an animation event is missed
        StartCoroutine(AttackResetFailsafe(2.5f));
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
            Debug.LogError("[DemonBoss DEBUG] ATTACK RESET FAILSAFE TRIGGERED! isAttacking was never set to false by OnAttackEnd().");
            isAttacking = false;
            if (player != null && !isStunned && !IsDead())
            {
                navAgent.isStopped = false;
            }
        }
    }

    private void PerformDodge()
    {
        isDodging = true;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator?.SetTrigger(AnimDodgeBackwards);
    }

    public void OnIntermissionEnd()
    {
        isIntermission = false;
        isAttacking = false;
        if (player != null && !isStunned && !IsDead())
        {
            navAgent.isStopped = false;
        }
    }

    public void OnDodgeEnd()
    {
        isDodging = false;
        if (player != null && !isStunned && !IsDead())
        {
            navAgent.isStopped = false;
        }
    }

    protected override bool CanPerformAction()
    {
        bool baseCheck = base.CanPerformAction();
        if (!baseCheck)
        {
            Debug.LogWarning($"[DemonBoss DEBUG] Base CanPerformAction is false! isAttacking: {isAttacking}, isStunned: {isStunned}, isCharging: {isCharging}, isInHitStun: {isInHitStun}, cooldown: {attackCooldownTimer:F2}");
        }
        return baseCheck && !isIntermission && !isDodging;
    }

    protected override float FacePlayerTurnSpeed => facePlayerTurnSpeed;
}
using System.Collections;
using UnityEngine;

public class DemonBoss : BaseEnemy
{
    [Header("Boss Settings")]
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
        }

        isEngaged = true;
        hasOpenedWithCharge = true;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
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

        if (EnemyCombatManager.Instance != null)
        {
            EnemyCombatManager.Instance.ReleaseAttackPermission(this);
        }

        animator?.SetTrigger(AnimIntermission);
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
}
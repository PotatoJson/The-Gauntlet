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

    /// <summary>
    /// Overriding TakeDamage to intercept the hit reaction and check for phase transitions.
    /// The DemonBoss does not stagger.
    /// </summary>
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

        //var healthBar = GetComponentInChildren<En>();
        //healthBar?.ShowHealthBar();

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

    /// <summary>
    /// Overriding HitStun to ensure nothing externally can force a hit stun on the boss.
    /// </summary>
    protected override void EnterHitStun()
    {
        // Do nothing. Bosses don't get staggered.
    }

    /// <summary>
    /// Replace default Engagement logic just in case to ensure charge isn't triggered.
    /// </summary>
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

    /// <summary>
    /// Prevent Charge Attacks entirely.
    /// </summary>
    public override void ChargeAttack()
    {
        // Do nothing. Boss has no charge.
    }

    /// <summary>
    /// Prevent Heavy Attacks entirely as the boss does not have any.
    /// </summary>
    public override void HeavyAttack()
    {
        // Do nothing. Boss has no heavy attack.
    }

    /// <summary>
    /// Overriding the main attack routing. We manage Phase 1, Phase 2, and Dodging here.
    /// </summary>
    public override void LightAttack()
    {
        if (!CanPerformAction() || isIntermission || isDodging)
        {
            // Optional: uncomment the line below if you want to see exactly what is blocking the attack, 
            // but beware it might spam the console if called every frame.
            // Debug.Log($"[DemonBoss] Cannot attack. CanPerformAction: {CanPerformAction()}, isIntermission: {isIntermission}, isDodging: {isDodging}");
            return;
        }

        // 10% chance to dodge when attempting to attack
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
            Debug.Log($"[DemonBoss] Phase 1 Attack triggered. Blend value: {randomIndex}");
            animator?.SetFloat(AnimLightRand, randomIndex);
            animator?.SetTrigger(AnimMeleeAttack);
        }
        else
        {
            float dualRandomIndex = Random.Range(0, 4) / 3f;
            Debug.Log($"[DemonBoss] Phase 2 Dual Wield Attack triggered. Blend value: {dualRandomIndex}");
            animator?.SetFloat(AnimDualRand, dualRandomIndex);
            animator?.SetTrigger(AnimDualWieldAttack);
        }
    }

    private void StartIntermission()
    {
        Debug.Log("[DemonBoss] StartIntermission() called. Halting boss and triggering 'Intermission' animation.");
        isIntermission = true;
        isPhase2 = true;

        // Enable Phase 2 dual-wield weapons
        if (leftSword != null) leftSword.SetActive(true);
        if (rightSword != null) rightSword.SetActive(true);

        // Interrupt current actions
        isAttacking = false;
        isDodging = false;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        if (EnemyCombatManager.Instance != null)
        {
            EnemyCombatManager.Instance.ReleaseAttackPermission(this);
        }

        animator?.SetTrigger(AnimIntermission);

        // FAILSAFE: This bypasses the need for an Animation Event in Unity.
        // It guarantees Phase 2 starts after `intermissionDuration` seconds.
        StartCoroutine(IntermissionTimer());
    }

    private IEnumerator IntermissionTimer()
    {
        // Wait for the animation to play out
        yield return new WaitForSeconds(intermissionDuration);

        // If we are still in intermission (event didn't fire), force it to end.
        if (isIntermission)
        {
            Debug.LogWarning("[DemonBoss] Intermission Timer finished! Forcing OnIntermissionEnd() logic.");
            OnIntermissionEnd();
        }
    }

    private void PerformDodge()
    {
        isDodging = true;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator?.SetTrigger(AnimDodgeBackwards);
    }

    #region Animation Events

    /// <summary>
    /// Ends the intermission gracefully. Automatically triggered by the Coroutine Failsafe now.
    /// </summary>
    public void OnIntermissionEnd()
    {
        Debug.Log("[DemonBoss] Boss Intermission Over! Resuming movement and Phase 2 attacks.");
        isIntermission = false;

        // Reset attack state just to be safe
        isAttacking = false; 

        if (player != null && !isStunned && !IsDead())  
        {
            navAgent.isStopped = false;
        }
    }

    /// <summary>
    /// Place this Animation Event at the end of the 'Dodge_Backwards' Animation!
    /// Alternatively, you can just use `OnAttackEnd` if you put it on the dodge too.
    /// </summary>
    public void OnDodgeEnd()
    {
        isDodging = false;

        if (player != null && !isStunned && !IsDead())
        {
            navAgent.isStopped = false;
        }
    }

    #endregion

    // Overriding CanPerformAction to include boss states
    protected override bool CanPerformAction()
    {
        return base.CanPerformAction() && !isIntermission && !isDodging;
    }
}
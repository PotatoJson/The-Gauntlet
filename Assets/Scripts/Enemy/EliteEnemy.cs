using UnityEngine;

public class EliteEnemy : BaseEnemy
{
    [Header("Elite Attack Damage")]
    [SerializeField] private float swordSwingDamage = 35f;
    [SerializeField] private float swordSlamDamage = 50f;

    [Header("Elite AI Settings")]
    [SerializeField] private float attackDistance = 3f;
    [SerializeField] private float chargeDistance = 10f;
    [SerializeField][Range(0f, 1f)] private float chargeChance = 0.5f;

    [Header("Sword Hitbox")]
    [SerializeField] private SwordHitbox swordHitbox;

    // Animation hashes for elite attacks
    protected static readonly int AnimSwordSwing = Animator.StringToHash("SwordSwing");
    protected static readonly int AnimSwordSlam = Animator.StringToHash("SwordSlam");

    private float aiDecisionTimer;
    private float aiDecisionInterval = 0.4f;

    protected override void Update()
    {
        base.Update();

        // Don't continue if dead, stunned, or in hit stun
        if (IsDead() || isStunned || isInHitStun) return;

        // Don't make decisions until engaged and opener is done
        if (!isEngaged || !hasOpenedWithCharge || isCharging || isAttacking) return;

        aiDecisionTimer -= Time.deltaTime;
        if (aiDecisionTimer <= 0f)
        {
            aiDecisionTimer = aiDecisionInterval + Random.Range(-0.1f, 0.1f);
            MakeDecision();
        }
    }

    /// <summary>
    /// Override so the base class doesn't fire LightAttack over our elite attacks.
    /// Lets MakeDecision() handle all attack choices for the Elite.
    /// </summary>
    protected override void ContinueCombat()
    {
        if (isAttacking || isCharging || isStunned || isInHitStun) return;

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

    private void MakeDecision()
    {
        if (isAttacking || isStunned) return;

        // Must have attack permission from the combat manager
        if (EnemyCombatManager.Instance != null &&
            !EnemyCombatManager.Instance.RequestAttackPermission(this))
        {
            return;
        }

        float distance = GetDistanceToPlayer();

        // Close enough to melee
        if (distance <= attackDistance)
        {
            float roll = Random.value;

            if (roll < 0.5f)
            {
                LightAttack();
            }
            else
            {
                SwordSlam();
            }
        }
        // Medium distance - chance to charge
        else if (distance <= chargeDistance && distance > attackDistance + 1.5f)
        {
            if (Random.value < chargeChance)
            {
                ChargeAttack();
            }
            else
            {
                // fallback to normal behavior (approach / continue combat)
                ChasePlayer();
                FacePlayer();
            }
        }
    }

    /// <summary>
    /// Override charge to use the Elite's sword slam as the charge finisher
    /// </summary>
    protected override void UpdateChargeAttack()
    {
        if (player == null)
        {
            EndCharge();
            return;
        }

        float distanceToPlayer = GetDistanceToPlayer();

        if (distanceToPlayer <= chargeStopDistance)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();

            isCharging = false;
            isAttacking = true;
            attackCooldownTimer = attackCooldown * 2f;

            FacePlayerImmediate();

            // Elite uses SwordSlam as the charge finisher
            Debug.Log($"{gameObject.name}: Charge reached player, triggering SwordSlam!");
            animator?.SetTrigger(AnimChargeAttack);
        }
        else
        {
            navAgent.speed = chargeSpeed;
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
        }
    }

    #region Elite Attacks

    /// <summary>
    /// Powerful sword slam - used standalone or as combo finisher
    /// </summary>
    public void SwordSlam()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown * 2f;
        navAgent.isStopped = true;

        animator?.SetTrigger(AnimSwordSlam);
    }
    #endregion

    #region Animation Events — Sword Hitbox Enable/Disable
    // Call these from animation events to toggle the sword collider.
    // Use EnableSwordSwingHitbox / EnableSwordSlamHitbox
    // at the start of the active frames, and DisableSwordHitbox at the end.

    public void EnableSwordSwingHitbox()
    {
        swordHitbox?.EnableHitbox(swordSwingDamage);
    }

    public void EnableSwordSlamHitbox()
    {
        swordHitbox?.EnableHitbox(swordSlamDamage);
    }

    public void DisableSwordHitbox()
    {
        swordHitbox?.DisableHitbox();
    }
    #endregion

    #region Animation Events — Attack End
    /// <summary>
    /// Call at end of sword swing animation
    /// </summary>
    public void OnSwordSwingEnd()
    {
        DisableSwordHitbox();
        OnAttackEnd();
    }

    /// <summary>
    /// Call at end of sword slam animation
    /// </summary>
    public void OnSwordSlamEnd()
    {
        DisableSwordHitbox();
        OnAttackEnd();
    }
    #endregion
}
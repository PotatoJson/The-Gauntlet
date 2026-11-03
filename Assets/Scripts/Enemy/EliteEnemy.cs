using System.Collections.Generic;
using UnityEngine;

public class EliteEnemy : BaseEnemy
{
    // The tutorial dummy just stands there; it must not hold the group's attack tokens.
    protected override bool UsesAttackTokens => !isTutorialDummy;


    [Header("Elite Attack Damage")]
    [SerializeField] private float swordSwingDamage = 35f;
    [SerializeField] private float swordSlamDamage = 50f;

    [Header("Elite AI Settings")]
    [SerializeField] private float attackDistance = 3f;
    [SerializeField] private float chargeDistance = 10f;
    [SerializeField][Range(0f, 1f)] private float chargeChance = 0.5f;
    [Header("Charge Leap")]
    [Tooltip("How far in front of the elite the player should be when the blade comes down.")]
    [SerializeField] private float leapStrikeDistance = 1.6f;
    [Tooltip("How far the leap animation travels before the blade comes down.")]
    [SerializeField] private float leapDistanceToStrike = 3.2f;
    [Tooltip("The most the leap can be stretched to reach a player further away.")]
    [SerializeField] private float maxLeapStretch = 1.75f;
    [Header("Tutorial Override")]
    public bool isTutorialDummy = false;
    [Header("Elite Buffing")]
    [SerializeField] private float buffRadius = 10f;
    [SerializeField] private int maxBuffTargets = 2;
    [SerializeField] private float buffDamageMultiplier = 1.25f;
    [SerializeField] private float buffAttackSpeedMultiplier = 1.2f;
    [SerializeField] private GameObject buffVfxPrefab;
    [SerializeField] private float buffInterval = 10f;

    [Header("Sword Hitbox")]
    [SerializeField] private SwordHitbox swordHitbox;

    // Animation hashes for elite attacks
    protected static readonly int AnimSwordSwing = Animator.StringToHash("LightAttack");
    protected static readonly int AnimSwordSlam = Animator.StringToHash("SwordSlam");

    private float aiDecisionTimer;
    private float aiDecisionInterval = 0.4f;
    private float buffTimer;

    private bool _leaping;
    private float _leapTravelled; // how much of the animation's own travel has played

    protected override void Update()
    {
        base.Update();

        // Don't continue if dead, stunned, or in hit stun
        if (IsDead() || isStunned || isInHitStun) return;

        if (isEngaged && buffInterval > 0f)
        {
            buffTimer -= Time.deltaTime;
            if (buffTimer <= 0f)
            {
                BuffNearbyEnemies();
                buffTimer = buffInterval;
            }
        }

        // Don't make decisions until engaged and opener is done
        if (!isEngaged || !hasOpenedWithCharge || isCharging || isAttacking || isTutorialDummy) return;

        aiDecisionTimer -= Time.deltaTime;
        if (aiDecisionTimer <= 0f)
        {
            aiDecisionTimer = aiDecisionInterval + Random.Range(-0.1f, 0.1f);
            MakeDecision();
        }
    }

    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= engagementRange)
        {
            bool justEngaged = !isEngaged;
            isEngaged = true;

            if (justEngaged)
            {
                buffTimer = 0f;
            }

            if (!hasOpenedWithCharge)
            {
                if (CanPerformAction())
                {
                    ChargeAttack();
                    hasOpenedWithCharge = true;
                }
            }
        }
        else if (isAware && !isAttacking && !isCharging)
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    /// <summary>
    /// Override so the base class doesn't fire LightAttack over our elite attacks.
    /// Lets MakeDecision() handle all attack choices for the Elite.
    /// </summary>
    protected override void ContinueCombat()
    {
        if (isAttacking || isCharging || isStunned || isInHitStun || isTutorialDummy) return;

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
            _leaping = true;
            _leapTravelled = 0f;
        }
        else
        {
            navAgent.speed = chargeSpeed;
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
        }
    }

    // The leap is a fixed length in the animation, so a close player got barged out of the way before
    // the blade came down. Stretch or shorten it, every frame, so the elite lands at striking distance.
    protected override Vector3 AdjustRootMotion(Vector3 delta)
    {
        if (!_leaping || !isAttacking || isInHitStun || player == null) return delta;

        Vector3 step = new Vector3(delta.x, 0f, delta.z);
        float natural = step.magnitude;
        if (natural < 0.0001f) return delta;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float gap = toPlayer.magnitude - leapStrikeDistance;
        float remaining = leapDistanceToStrike - _leapTravelled;
        _leapTravelled += natural;

        float scale;
        if (remaining > 0.05f)
        {
            // Track the player through the wind-up only, so a late roll to the side still dodges it.
            if (remaining > 1f) FacePlayer();
            scale = Mathf.Clamp(gap / remaining, 0f, maxLeapStretch);
        }
        else
        {
            scale = gap > 0f ? 1f : 0f; // after the strike: finish the step, but never walk into the player
        }

        return step * scale + Vector3.up * delta.y;
    }

    public override void OnAttackEnd()
    {
        _leaping = false;
        base.OnAttackEnd();
    }

    #region Elite Attacks

    /// <summary>
    /// Powerful sword slam - used standalone or as combo finisher
    /// </summary>
    protected override string AttackEvent => "event:/Enemies/Elite_Attack";

    public void SwordSlam()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown * 2f;
        navAgent.isStopped = true;

        animator?.SetTrigger(AnimSwordSlam);
        PlayAttackSound();
    }

    public override void LightAttack()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        animator?.SetTrigger(AnimSwordSwing);
        PlayAttackSound();
    }
    #endregion

    #region Animation Events ?Sword Hitbox Enable/Disable
    // Call these from animation events to toggle the sword collider.
    // Use EnableSwordSwingHitbox / EnableSwordSlamHitbox
    // at the start of the active frames, and DisableSwordHitbox at the end.

    public void EnableSwordSwingHitbox()
    {
        swordHitbox?.EnableHitbox(swordSwingDamage, lightAttackPoiseDamage);
    }

    public void EnableSwordSlamHitbox()
    {
        swordHitbox?.EnableHitbox(swordSlamDamage, heavyAttackPoiseDamage);
    }

    public void DisableSwordHitbox()
    {
        swordHitbox?.DisableHitbox();
    }
    #endregion

    #region Animation Events ?Attack End
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
    }
}
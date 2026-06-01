using System.Collections;
using UnityEngine;

public class ShieldEnemy : BaseEnemy
{
    [Header("Shield Settings")]
    [SerializeField] private float shieldBashDamage = 20f;
    [SerializeField] private float shieldBashInterval = 5f;
    [SerializeField] private float shieldBashIntervalVariance = 1f;
    [SerializeField] private float shieldRecoveryDuration = 3f;

    [Header("Shield Bash Hitbox")]
    [SerializeField] private Vector3 shieldBashHitboxOffset = new Vector3(0f, 1f, 1.5f);
    [SerializeField] private Vector3 shieldBashHitboxSize = new Vector3(3f, 2f, 2.5f);

    [Header("AI Settings (Broken Shield)")]
    [SerializeField] private float attackDistance = 2.5f;
    [SerializeField][Range(0f, 1f)] private float heavyAttackChance = 0.3f;

    [Header("Animation Settings")]
    [SerializeField] private string locomotionStateName = "Locomotion";
    [SerializeField] private float locomotionBlendDuration = 0.1f;

    [Header("Shield Block Settings")]
    [SerializeField] private float shieldBlockWindow = 0.1f;

    private bool shieldBroken;
    private bool isShieldRecovering;
    private bool isBlocking;
    private float shieldBashTimer;

    private float aiDecisionTimer;
    private float aiDecisionInterval = 0.4f;

    private float lastShieldBlockTime;
    private int lastShieldBlockHitboxId = -1;
    private int locomotionStateHash;

    protected static readonly int AnimBlock = Animator.StringToHash("Block");
    protected static readonly int AnimBlockHit = Animator.StringToHash("BlockHit");

    protected override void Start()
    {
        base.Start();
        ResetShieldBashTimer();
        locomotionStateHash = Animator.StringToHash(locomotionStateName);
    }

    protected override void Update()
    {
        if (!isEngaged)
        {
            isBlocking = false;
        }

        if (shieldBroken)
        {
            UpdateBrokenBehavior();
            return;
        }

        base.Update();

        if (IsDead() || isStunned || isInHitStun) return;
        if (!isAware || !isEngaged) return;

        if (!isAttacking)
        {
            BeginBlock();
        }

        if (isAttacking || isShieldRecovering) return;

        float distance = GetDistanceToPlayer();
        if (distance > attackRange)
        {
            InterruptBlockForMovement();
            ChasePlayer();
            FacePlayer();
            return;
        }

        shieldBashTimer -= Time.deltaTime;
        if (shieldBashTimer <= 0f)
        {
            ResetShieldBashTimer();
            ShieldBash();
        }
    }

    private void UpdateBrokenBehavior()
    {
        base.Update();

        if (IsDead() || isStunned || isInHitStun) return;
        if (!isAware || !isEngaged) return;
        if (isAttacking) return;

        aiDecisionTimer -= Time.deltaTime;
        if (aiDecisionTimer <= 0f)
        {
            aiDecisionTimer = aiDecisionInterval + Random.Range(-0.1f, 0.1f);
            MakeBasicDecision();
        }
    }

    protected override void ContinueCombat()
    {
        if (shieldBroken)
        {
            return;
        }

        if (isAttacking || isStunned || isInHitStun || isShieldRecovering) return;

        float distance = GetDistanceToPlayer();
        if (distance > attackRange)
        {
            InterruptBlockForMovement();
            ChasePlayer();
        }
        else
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        FacePlayer();
        BeginBlock();
    }

    protected override void CheckEngagement()
    {
        if (shieldBroken)
        {
            base.CheckEngagement();
            return;
        }

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

    private void MakeBasicDecision()
    {
        if (isAttacking || isStunned) return;

        float distance = GetDistanceToPlayer();

        if (distance <= attackDistance)
        {
            HeavyAttack();
        }
        else
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    private void ShieldBash()
    {
        if (!CanPerformAction()) return;

        if (shieldBroken)
        {
            HeavyAttack();
            return;
        }

        isBlocking = false;
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        FacePlayerImmediate();
        animator?.SetTrigger(AnimLightAttack);
    }

    private void BeginBlock()
    {
        if (isBlocking) return;

        isBlocking = true;
        animator?.SetTrigger(AnimBlock);
    }

    private void InterruptBlockForMovement()
    {
        if (!isBlocking) return;

        isBlocking = false;
        animator?.ResetTrigger(AnimBlock);

        if (animator == null || string.IsNullOrEmpty(locomotionStateName)) return;
        if (!animator.HasState(0, locomotionStateHash)) return;

        animator.CrossFadeInFixedTime(locomotionStateHash, locomotionBlendDuration);
    }

    private void ResetShieldBashTimer()
    {
        shieldBashTimer = shieldBashInterval + Random.Range(-shieldBashIntervalVariance, shieldBashIntervalVariance);
        if (shieldBashTimer < 0.5f) shieldBashTimer = 0.5f;
    }

    public void OnShieldBroken()
    {
        if (shieldBroken) return;

        shieldBroken = true;
        isShieldRecovering = false;
        isAttacking = false;
        isBlocking = false;

        animator?.ResetTrigger(AnimLightAttack);
        animator?.ResetTrigger(AnimBlock);

        ResetShieldBashTimer();
        animator?.SetTrigger(AnimBlockHit);
    }

    public void OnShieldBashHit()
    {
        if (shieldBroken) return;

        bool hitPlayer = TryDamagePlayerHitbox(0f, 100, shieldBashHitboxOffset, shieldBashHitboxSize);

    }

    public void OnShieldBashEnd()
    {
        if (shieldBroken) return;
        isAttacking = false;
        StartCoroutine(ShieldRecoveryRoutine());
    }

    private IEnumerator ShieldRecoveryRoutine()
    {
        isShieldRecovering = true;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        yield return new WaitForSeconds(shieldRecoveryDuration);

        isShieldRecovering = false;
        OnAttackEnd();
    }

    public override void ApplyStun(float duration)
    {
        isBlocking = false;
        base.ApplyStun(duration);
    }

    public void RegisterShieldHit(HitboxController hitbox)
    {
        if (shieldBroken || hitbox == null) return;

        lastShieldBlockHitboxId = hitbox.GetInstanceID();
        lastShieldBlockTime = Time.time;
    }

    public bool IsShieldBlockingHit(HitboxController hitbox)
    {
        if (shieldBroken || hitbox == null) return false;
        if (lastShieldBlockHitboxId != hitbox.GetInstanceID()) return false;

        return Time.time - lastShieldBlockTime <= shieldBlockWindow;
    }
}
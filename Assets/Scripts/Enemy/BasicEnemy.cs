using UnityEngine;

public class BasicEnemy : BaseEnemy
{
    [Header("AI Settings")]
    [SerializeField] private float attackDistance = 2.5f;
    [SerializeField] private float chargeDistance = 8f;
    [SerializeField][Range(0f, 1f)] private float heavyAttackChance = 0.3f;
    [SerializeField][Range(0f, 1f)] private float chargeChance = 0.5f;

    private float aiDecisionTimer;
    private float aiDecisionInterval = 0.4f;

    protected override void Update()
    {
        base.Update();

        // Don't continue if dead, stunned, in hit stun, or dashing back
        if (IsDead() || isStunned || isInHitStun) return;

        // Don't do anything until aware of player
        if (!isAware) return;

        // Don't do anything until engaged
        if (!isEngaged) return;

        // Wait for opening charge to complete before making decisions
        if (!hasOpenedWithCharge || isCharging || isAttacking) return;

        aiDecisionTimer -= Time.deltaTime;
        if (aiDecisionTimer <= 0f)
        {
            aiDecisionTimer = aiDecisionInterval + Random.Range(-0.1f, 0.1f);
            MakeDecision();
        }
    }

    private void MakeDecision()
    {
        if (isAttacking || isStunned || isCharging) return;

        float distance = GetDistanceToPlayer();

        // Close enough to melee
        if (distance <= attackDistance)
        {
            float roll = Random.value;

            if (roll < heavyAttackChance)
            {
                HeavyAttack();
            }
            else
            {
                // Normal attack pattern - light attack with random animation
                LightAttack();
            }
        }
        // Medium distance → chance to charge
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
}
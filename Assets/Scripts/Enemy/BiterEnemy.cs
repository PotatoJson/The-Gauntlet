using UnityEngine;

public class BiterEnemy : BaseEnemy
{
    [Header("Biter Enemy Settings")]
    [SerializeField] private float biteRange = 2.5f;

    private float aiDecisionTimer;
    private float aiDecisionInterval = 0.4f;

    protected override void Start()
    {
        base.Start();
        // Immediately tell the base class we've "opened" so it never forces a charge attack on us
        hasOpenedWithCharge = true;
    }

    protected override void Update()
    {
        base.Update();

        if (IsDead() || isStunned || isInHitStun) return;

        // Don't do anything until aware and engaged
        if (!isAware || !isEngaged) return;

        if (isAttacking) return;

        aiDecisionTimer -= Time.deltaTime;
        if (aiDecisionTimer <= 0f)
        {
            aiDecisionTimer = aiDecisionInterval + Random.Range(-0.1f, 0.1f);
            MakeDecision();
        }
    }

    /// <summary>
    /// Override to prevent BaseEnemy from forcing standard melee engagement.
    /// </summary>
    protected override void ContinueCombat()
    {
        // Leaving this blank stops the base combat loop (which forces Light/Heavy attacks).
        // It's handled entirely by MakeDecision() for this enemy.
    }

    /// <summary>
    /// Override the engagement check so we never attempt an opener charge attack
    /// </summary>
    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= engagementRange)
        {
            isEngaged = true;
            hasOpenedWithCharge = true; // skip opener charge entirely
        }
        else if (isAware && !isAttacking && !isCharging)
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    private void MakeDecision()
    {
        if (isAttacking || isStunned) return;

        float distance = GetDistanceToPlayer();

        if (distance > biteRange)
        {
            ChasePlayer();
            FacePlayer();
        }
        else
        {
            if (navAgent != null)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
            }

            FacePlayerImmediate();
            LightAttack();
        }
    }

    public void OnDieAnimationEnd()
    {
        Destroy(gameObject);
    }
}
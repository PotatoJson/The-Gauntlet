using UnityEngine;
using UnityEngine.AI;

public class SpinningEnemy : BaseEnemy
{
    [Header("Spin Attack Settings")]
    [SerializeField] private float spinMoveSpeed = 4f;
    [SerializeField] private float spinAgroRange = 15f;

    [Header("Spin Damage Settings")]
    [SerializeField] private float spinDamage = 10f;
    [SerializeField] private float damageRange = 2f;
    [SerializeField] private float damageTickRate = 0.5f; // How often the player takes damage while touching the enemy

    private bool isSpinning = false;
    private float damageTimer = 0f;

    protected override void Update()
    {
        // Let BaseEnemy do its normal updates (which natively sets speed parameters > 0 if moving)
        base.Update();

        // Handle moving towards the player while spinning
        if (isSpinning && !isStunned && !isInHitStun && !IsDead())
        {
            if (player != null && navAgent.isOnNavMesh)
            {
                // Constantly override the target to slowly chase the player
                navAgent.updatePosition = true;
                navAgent.isStopped = false;
                navAgent.speed = spinMoveSpeed;
                navAgent.SetDestination(player.position);

                // Keep facing the player immediately
                FacePlayerImmediate();

                // Attempt to damage the player continuously while they are within the spin hitbox
                DamagePlayerIfClose();
            }

            // Stop spinning and return to idle if player gets out of range
            if (GetDistanceToPlayer() > spinAgroRange)
            {
                StopSpinning();
            }
        }
    }

    private void DamagePlayerIfClose()
    {
        float distance = GetDistanceToPlayer();

        if (distance <= damageRange)
        {
            damageTimer -= Time.deltaTime;
            if (damageTimer <= 0f)
            {
                // TryDamagePlayer is a base class method that handles distance checks and player health mapping
                TryDamagePlayer(spinDamage, damageRange + 0.5f);
                damageTimer = damageTickRate; // Reset timer for the next damage tick
            }
        }
        else
        {
            // Reset the timer so it damages immediately upon touching the player again
            damageTimer = 0f;
        }
    }

    // Override engagement to skip the base "Opener Charge" logic and replace it with spin aggro
    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= spinAgroRange)
        {
            isEngaged = true;
            hasOpenedWithCharge = true; // Prevent base charge behavior

            if (!isSpinning && CanPerformAction())
            {
                StartSpinning();
            }
        }
        else
        {
            // Player is out of range, stay idle
            GoIdle();
        }
    }

    protected override void ContinueCombat()
    {
        if (isStunned || isInHitStun || IsDead()) return;

        float distance = GetDistanceToPlayer();

        if (distance <= spinAgroRange)
        {
            if (!isSpinning && CanPerformAction())
            {
                StartSpinning();
            }
        }
        else
        {
            StopSpinning();
        }
    }

    private void StartSpinning()
    {
        isSpinning = true;
        damageTimer = 0f; // Ready to damage immediately upon contact

        if (navAgent.isOnNavMesh)
        {
            navAgent.updatePosition = true;
            navAgent.speed = spinMoveSpeed;
            navAgent.isStopped = false;
        }
    }

    private void StopSpinning()
    {
        if (!isSpinning) return;

        isSpinning = false;
        GoIdle();
    }

    private void GoIdle()
    {
        isAware = false;
        isEngaged = false;

        if (navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.speed = chaseSpeed; // Reset to default speed
            navAgent.velocity = Vector3.zero;
        }
    }

    protected override bool CanPerformAction()
    {
        return base.CanPerformAction() && !isSpinning;
    }

    // --- Damage Override Logic ---

    /// <summary>
    /// Overridden to prevent standard damage calculation since the body is invulnerable.
    /// </summary>
    public override void TakeDamage(float damage)
    {
        Debug.Log($"{gameObject.name}: Ignored general body damage.");
        // Do nothing!
    }

    /// <summary>
    /// PUBLIC METHOD: Call this from the hit box script attached to the enemy's head.
    /// </summary>
    public void TakeWeakPointDamage(float damage)
    {
        Debug.Log($"{gameObject.name}: Weak point hit for {damage} damage!");

        // Stop the spin sequence if severely damaged/stunned 
        StopSpinning();

        // Pass the damage down to the base enemy to process health reduction, hit reactions, and death.
        base.TakeDamage(damage);
    }

    // --- Disable Base Attack Logic so the Animator doesn't get interrupted ---
    public override void ChargeAttack() { }
    public override void LightAttack() { }
    public override void HeavyAttack() { }
}
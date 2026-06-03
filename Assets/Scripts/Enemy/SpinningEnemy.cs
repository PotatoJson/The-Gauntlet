using UnityEngine;

public class SpinningEnemy : BaseEnemy
{
    [Header("Spin Attack Damage")]
    [SerializeField] private float spinDamage = 10f;

    [Header("Spin Aggro Settings")]
    [SerializeField] private float spinAgroRange = 15f;

    [Header("Axe Spawn")]
    [SerializeField] private GameObject axePrefab;
    [SerializeField] private Transform axeSocket;

    [Header("Axe Hitbox")]
    [SerializeField] private AxeHitbox axeHitbox;

    private GameObject spawnedAxe;
    private bool hasAggroPosePlayed;
    private bool movementUnlocked;

    private static readonly int AnimAggroPose = Animator.StringToHash("AggroPose");

    protected override void Update()
    {
        base.Update();

        if (!isAware && !isEngaged && !isStunned && !isAttacking && !isCharging && !isInHitStun)
        {
            if (navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
        }
    }

    protected override void CheckAwareness()
    {
        bool wasAware = isAware;
        base.CheckAwareness();

        if (!wasAware && isAware)
        {
            TriggerAggroPose();
        }
    }

    protected override void CheckEngagement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        if (distance <= spinAgroRange)
        {
            bool justEngaged = !isEngaged;
            isEngaged = true;
            hasOpenedWithCharge = true;

            if (justEngaged)
            {
                SpawnAxeIfNeeded();
            }
        }
        else if (isAware && movementUnlocked)
        {
            isEngaged = true;
            hasOpenedWithCharge = true;
            ChasePlayer();
            FacePlayer();
        }
    }

    protected override void ContinueCombat()
    {
        if (player == null || isStunned || isInHitStun || IsDead()) return;

        if (!movementUnlocked)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            return;
        }

        float distance = GetDistanceToPlayer();

        if (distance <= spinAgroRange)
        {
            FacePlayer();
            LightAttack();
        }
        else
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    private void TriggerAggroPose()
    {
        if (hasAggroPosePlayed || animator == null) return;

        hasAggroPosePlayed = true;
        movementUnlocked = false;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        SpawnAxeIfNeeded();
        animator.SetTrigger(AnimAggroPose);
    }

    // Animation event at the end of the aggro pose clip
    public void OnAggroPoseComplete()
    {
        movementUnlocked = true;
        isAware = true;
        isEngaged = true;
        hasOpenedWithCharge = true;

        if (player == null || isStunned || isInHitStun || !navAgent.isOnNavMesh) return;

        navAgent.isStopped = false;
        navAgent.speed = chaseSpeed;
        ChasePlayer();
        FacePlayer();
    }

    private void SpawnAxeIfNeeded()
    {
        if (spawnedAxe != null) return;

        if (axePrefab == null || axeSocket == null)
        {
            Debug.LogWarning($"{gameObject.name}: Axe spawn skipped (axePrefab or axeSocket missing).");
            return;
        }

        spawnedAxe = Instantiate(axePrefab, axeSocket);
        spawnedAxe.transform.localPosition = Vector3.zero;
        spawnedAxe.transform.localRotation = Quaternion.identity;

        axeHitbox = spawnedAxe.GetComponentInChildren<AxeHitbox>(true);
        if (axeHitbox == null)
        {
            Debug.LogWarning($"{gameObject.name}: Spawned axe has no AxeHitbox in children.");
        }
        else
        {
            Debug.Log($"{gameObject.name}: AxeHitbox found on spawned axe ({axeHitbox.gameObject.name}).");
        }
    }

    public void EnableAxeHitbox()
    {
        axeHitbox?.EnableHitbox(spinDamage);
    }

    public void DisableAxeHitbox()
    {
        axeHitbox?.DisableHitbox();
    }

    public void OnSpinAttackEnd()
    {
        DisableAxeHitbox();
        OnAttackEnd();
    }


    public override void ChargeAttack() { }
}
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class BaseEnemy : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] protected float maxHealth = 100f;
    public float currentHealth;

    [Header("Rewards")]
    [Tooltip("How much xp this specific enemy drops")]
    [SerializeField] protected float xpValue;

    [Header("Movement")]
    [SerializeField] protected float chaseSpeed = 4f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float chargeSpeed = 5f;
    [SerializeField] protected float chargeStopDistance = 2f;

    [Header("Idle Wandering")]
    [SerializeField] private bool enableIdleWander = true;
    [SerializeField] private float idleWanderRadius = 4f;
    [SerializeField] private float idleWanderSpeed = 1.5f;
    [SerializeField] private float idleWanderInterval = 2.5f;
    [SerializeField] private float idleWanderIntervalVariance = 0.5f;

    [Header("Awareness & Engagement")]
    [SerializeField] protected float awarenessRange = 25f;
    [SerializeField] protected float engagementRange = 15f;

    [Header("Line of Sight")]
    [Tooltip("Layers that block enemy vision (e.g., walls/environment).")]
    [SerializeField] private LayerMask lineOfSightObstructionMask = 0;
    [SerializeField] private float lineOfSightHeight = 1.6f;

    [Header("Attack Damage")]
    [SerializeField] protected float lightAttackDamage = 10f;
    [SerializeField] protected float heavyAttackDamage = 25f;
    [SerializeField] protected float chargeAttackDamage = 20f;

    [Header("Attack Poise Damage")]
    [SerializeField] protected int lightAttackPoiseDamage = 10;
    [SerializeField] protected int heavyAttackPoiseDamage = 25;
    [SerializeField] protected int chargeAttackPoiseDamage = 20;

    [Header("Hitbox Settings (Virtual Hitboxes)")]
    [Tooltip("Layer mask containing the Player")]
    [SerializeField] protected LayerMask playerLayerMask = ~0; // Default to all layers, but you should set this to 'Player' in inspector
    [SerializeField] protected Vector3 lightAttackHitboxOffset = new Vector3(0, 1f, 1f);
    [SerializeField] protected Vector3 lightAttackHitboxSize = new Vector3(2f, 2f, 2f);
    [SerializeField] protected Vector3 heavyAttackHitboxOffset = new Vector3(0, 1f, 1.5f);
    [SerializeField] protected Vector3 heavyAttackHitboxSize = new Vector3(2.5f, 2f, 3f);
    [SerializeField] protected Vector3 chargeAttackHitboxOffset = new Vector3(0, 1f, 1.5f);
    [SerializeField] protected Vector3 chargeAttackHitboxSize = new Vector3(3f, 2f, 3f);

    [Header("Enemy Trap")]
    [SerializeField] private bool trapEnemiesOnHit = true;
    [SerializeField] private float enemyTrapDuration = 2f;
    [SerializeField] private LayerMask enemyLayerMask = ~0;

    [Header("Timing")]
    [SerializeField] protected float stunDuration = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;

    [Header("Hit Stun")]
    [SerializeField] protected float maxHitStunDuration = 5f;
    [SerializeField] protected float hitStunResetTime = 1f;
    protected float hitStunTimer;
    protected float timeSinceLastHit;
    protected bool isInHitStun;

    [Header("Hit Immunity")]
    [SerializeField] protected float hitImmunityDuration = 5f; 
    protected bool isHitImmune;
    protected float hitImmuneTimer;

    [Header("Stun Meter")]
    [SerializeField] protected float maxStunMeter = 100f;
    [SerializeField] protected float stunDecayRate = 10f;
    protected float currentStunMeter;

    [Header("Recovery")]
    [SerializeField] protected float hitRecoveryTime = 0.5f;
    protected bool isRecovering;

    [Header("References")]
    [SerializeField] protected Transform player;
    [SerializeField] protected Animator animator;

    [Header("Status Effects Things")]
    protected float _currentSpeedMultiplier = 1f;
    private float _storedNavSpeed;

    // Components
    protected NavMeshAgent navAgent;

    // State
    protected bool isAttacking;
    protected bool isCharging;
    protected bool isStunned;
    protected bool isAware;
    protected bool isEngaged;
    protected bool hasOpenedWithCharge;
    protected float attackCooldownTimer;
    private bool isBuffed;

    private Vector3 idleWanderCenter;
    private float idleWanderTimer;
    private bool isIdleWandering;

    // Animation parameter hashes
    protected static readonly int AnimLightAttack = Animator.StringToHash("LightAttack");
    protected static readonly int AnimHeavyAttack = Animator.StringToHash("HeavyAttack");
    protected static readonly int AnimChargeAttack = Animator.StringToHash("ChargeAttack");
    protected static readonly int AnimLightRandom = Animator.StringToHash("LightRandom");
    protected static readonly int AnimHitReaction = Animator.StringToHash("HitReaction");
    protected static readonly int AnimDie = Animator.StringToHash("Die");
    protected static readonly int AnimSpeed = Animator.StringToHash("Speed");
    protected static readonly int AnimStunned = Animator.StringToHash("Stunned");

    private PlayerHealth playerHealth;
    protected PlayerManager playerManager;

    //death event
    public static event System.Action<BaseEnemy> OnAnyEnemyDied;

    protected virtual void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerManager = player.GetComponent<PlayerManager>();
        }
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        navAgent.speed = chaseSpeed;
        navAgent.isStopped = true;
        navAgent.updateRotation = false;

        isAware = false;
        isEngaged = false;
        hasOpenedWithCharge = false;

        idleWanderCenter = transform.position;
        idleWanderTimer = GetNextIdleWanderDelay();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    protected virtual void Update()
    {
        if (currentHealth <= 0) return;

        if (isAware || isEngaged)
        {
            playerManager?.SetInCombat();
        }

        if (isHitImmune)
        {
            hitImmuneTimer -= Time.deltaTime;
            if (hitImmuneTimer <= 0f) isHitImmune = false;
        }

        if (isInHitStun)
        {
            hitStunTimer += Time.deltaTime;
            timeSinceLastHit += Time.deltaTime;

            if (hitStunTimer >= maxHitStunDuration || timeSinceLastHit >= hitStunResetTime)
            {
                ExitHitStunWithImmunity();
            }
            return;
        }

        if (attackCooldownTimer > 0)
            attackCooldownTimer -= Time.deltaTime;

        if (!isAware) CheckAwareness();
        else if (!isEngaged || !hasOpenedWithCharge) CheckEngagement();
        else ContinueCombat();

        if (!isAware && !isEngaged && !isStunned && !isAttacking && !isCharging && !isInHitStun)
        {
            UpdateIdleWander();

            if (isIdleWandering)
            {
                FaceMovementDirection();
            }
        }
        else
        {
            StopIdleWander(false);
        }

        if (isCharging) UpdateChargeAttack();

        if (isAware && !isStunned && !isAttacking && !isInHitStun)
        {
            FacePlayer();
        }

        UpdateAnimatorParameters();
    }

    public bool IsBlockingOrStunned() => isStunned || isInHitStun;

    protected void UpdateAnimatorParameters()
    {
        if (animator == null) return;
        Vector3 worldVelocity = navAgent.velocity;
        float rawSpeed = worldVelocity.magnitude;
        Vector3 localVelocityDir = transform.InverseTransformDirection(worldVelocity.normalized);
        animator.SetFloat(AnimSpeed, rawSpeed);
        animator.SetFloat("VelocityX", localVelocityDir.x);
        animator.SetFloat("VelocityZ", localVelocityDir.z);
    }

    private void OnAnimatorMove()
    {
        if (animator == null) return;
        if (isAttacking || isInHitStun)
        {
            if (navAgent.updatePosition) navAgent.updatePosition = false;
            transform.position += animator.deltaPosition;
            navAgent.nextPosition = transform.position;
        }
        else
        {
            if (!navAgent.updatePosition)
            {
                navAgent.nextPosition = transform.position;
                navAgent.updatePosition = true;
            }
        }
    }

    protected virtual void ContinueCombat()
    {
        if (isAttacking || isCharging || isStunned || isInHitStun) return;
        if (!HasLineOfSightToPlayer())
        {
            StopChasing();
            return;
        }

        float distance = GetDistanceToPlayer();
        if (distance <= attackRange)
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

    protected virtual void CheckAwareness()
    {
        if (player == null) return;
        if (GetDistanceToPlayer() <= awarenessRange && HasLineOfSightToPlayer())
        {
            isAware = true;
            StopIdleWander(true);
            navAgent.isStopped = false;
            playerManager?.SetInCombat();
        }
    }

    protected virtual void CheckEngagement()
    {
        if (player == null) return;
        float distance = GetDistanceToPlayer();
        if (distance <= engagementRange && HasLineOfSightToPlayer())
        {
            isEngaged = true;
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
            if (HasLineOfSightToPlayer())
            {
                ChasePlayer();
                FacePlayer();
            }
            else
            {
                StopChasing();
            }
        }
    }

    protected float GetDistanceToPlayer() => player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

    private bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;

        Vector3 origin = transform.position + Vector3.up * lineOfSightHeight;
        Vector3 target = player.position + Vector3.up * lineOfSightHeight;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f) return true;

        return !Physics.Raycast(origin, direction.normalized, distance, lineOfSightObstructionMask, QueryTriggerInteraction.Ignore);
    }

    private void StopChasing()
    {
        if (!navAgent.isOnNavMesh) return;
        navAgent.isStopped = true;
        navAgent.ResetPath();
        navAgent.velocity = Vector3.zero;
    }

    protected void ChasePlayer()
    {
        if (isAttacking || isStunned || isCharging || isInHitStun || player == null) return;
        if (!HasLineOfSightToPlayer())
        {
            StopChasing();
            return;
        }

        if (navAgent.isStopped) navAgent.isStopped = false;
        navAgent.speed = chaseSpeed;

        playerManager?.SetInCombat();

        if (Vector3.SqrMagnitude(navAgent.destination - player.position) > 1.5f)
            navAgent.SetDestination(player.position);
    }

    protected virtual float FacePlayerTurnSpeed => 10f;

    protected void FacePlayer()
    {
        if (player == null) return;
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), FacePlayerTurnSpeed * Time.deltaTime);
    }

    private void FaceMovementDirection()
    {
        if (!navAgent.isOnNavMesh) return;

        Vector3 velocity = navAgent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude <= 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
    }

    protected void FacePlayerImmediate()
    {
        if (player == null) return;
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private float GetNextIdleWanderDelay()
    {
        if (idleWanderIntervalVariance <= 0f) return idleWanderInterval;
        return idleWanderInterval + Random.Range(-idleWanderIntervalVariance, idleWanderIntervalVariance);
    }

    private void UpdateIdleWander()
    {
        if (!enableIdleWander || !navAgent.isOnNavMesh) return;

        idleWanderTimer -= Time.deltaTime;

        bool reachedDestination = isIdleWandering &&
            !navAgent.pathPending &&
            navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f;

        if (idleWanderTimer <= 0f || reachedDestination)
        {
            Vector3 target = idleWanderCenter + Random.insideUnitSphere * idleWanderRadius;
            target.y = idleWanderCenter.y;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, idleWanderRadius, NavMesh.AllAreas))
            {
                navAgent.speed = idleWanderSpeed;
                navAgent.isStopped = false;
                navAgent.SetDestination(hit.position);
                isIdleWandering = true;
            }
            else if (isIdleWandering)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                isIdleWandering = false;
            }

            idleWanderTimer = GetNextIdleWanderDelay();
        }
    }

    private void StopIdleWander(bool resetPath)
    {
        if (!isIdleWandering) return;

        isIdleWandering = false;
        idleWanderTimer = GetNextIdleWanderDelay();

        if (resetPath && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        if (!isCharging)
        {
            navAgent.speed = chaseSpeed;
        }
    }

    #region Combat Actions
    public virtual void LightAttack()
    {
        if (!CanPerformAction()) return;
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator?.SetFloat(AnimLightRandom, Random.Range(0, 2));
        animator?.SetTrigger(AnimLightAttack);
    }

    public virtual void HeavyAttack()
    {
        if (!CanPerformAction()) return;
        isAttacking = true;
        attackCooldownTimer = attackCooldown * 1.5f;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator?.SetTrigger(AnimHeavyAttack);
    }

    public virtual void ChargeAttack()
    {
        if (!CanPerformAction() || player == null) return;
        isCharging = true;
        attackCooldownTimer = attackCooldown * 1.8f;
        navAgent.isStopped = false;
        navAgent.updatePosition = true;
        navAgent.speed = chargeSpeed;
        navAgent.SetDestination(player.position);
    }

    protected virtual void UpdateChargeAttack()
    {
        if (player == null) { EndCharge(); return; }
        if (GetDistanceToPlayer() <= chargeStopDistance)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
            isCharging = false;
            isAttacking = true;
            FacePlayerImmediate();
            animator?.SetTrigger(AnimChargeAttack);
        }
        else
        {
            navAgent.speed = chargeSpeed;
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
        }
    }

    protected void EndCharge()
    {
        isCharging = false;
        navAgent.velocity = Vector3.zero;
        navAgent.speed = chaseSpeed;
    }

    protected virtual bool CanPerformAction() => !isAttacking && !isStunned && !isCharging && !isInHitStun && attackCooldownTimer <= 0;
    #endregion

    #region Damage, Health & Hitboxes
    public virtual void TakeDamage(float damage)
    {
        //Force the enemy to report its health
        Debug.Log($"<color=yellow>Enemy {gameObject.name} hit! Current Health is: {currentHealth}</color>");

        if (IsDead()) 
        {
            Debug.LogWarning("Enemy is already dead! Skipping damage and sound.");
            return;
        }

        playerManager?.SetInCombat();
        
        if (!isAware) { isAware = true; navAgent.isStopped = false; }
        if (!isEngaged) { isEngaged = true; hasOpenedWithCharge = true; }

        StopIdleWander(true);

        currentHealth -= damage;
        GetComponentInChildren<EnemyHealthBar>()?.ShowHealthBar();

        if (currentHealth <= 0) { Die(); return; }
        if (isHitImmune || isCharging) return;

        isAttacking = false;
        isCharging = false;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        EnterHitStun();
    }

    protected virtual void Die()
    {
        StopAllCoroutines();
        isAttacking = false;
        isCharging = false;
        isRecovering = false;
        isInHitStun = false;
        isHitImmune = false;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.enabled = false;
        //enemy death event for gem event manager
        OnAnyEnemyDied?.Invoke(this);

        animator?.SetTrigger(AnimDie);
        gameObject.layer = LayerMask.NameToLayer("Default");
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        var healthBar = GetComponentInChildren<EnemyHealthBar>();
        if (healthBar != null) healthBar.gameObject.SetActive(false);

        if(ProgressBarCircle.Instance != null)
        {
            ProgressBarCircle.Instance.AddExperience(xpValue);
        }

        var dissolve = GetComponent<DissolveExample.DissolveChilds>();
        if (dissolve != null) dissolve.StartDissolve();
        else Destroy(gameObject, 3f);

        enabled = false;
    }

    public bool IsDead() => currentHealth <= 0;

    // ============== UPDATED HITBOX LOGIC ==============
    public void OnLightAttackHit() => TryDamagePlayerHitbox(lightAttackDamage, lightAttackPoiseDamage, lightAttackHitboxOffset, lightAttackHitboxSize);
    public void OnHeavyAttackHit() => TryDamagePlayerHitbox(heavyAttackDamage, heavyAttackPoiseDamage, heavyAttackHitboxOffset, heavyAttackHitboxSize);
    public void OnChargeAttackHit() => TryDamagePlayerHitbox(chargeAttackDamage, chargeAttackPoiseDamage, chargeAttackHitboxOffset, chargeAttackHitboxSize);

    protected bool TryDamagePlayerHitbox(float damage, int poiseDamage, Vector3 localOffset, Vector3 boxSize)
    {
        Vector3 center = transform.TransformPoint(localOffset);
        int combinedMask = playerLayerMask | enemyLayerMask;
        Collider[] hits = Physics.OverlapBox(center, boxSize / 2f, transform.rotation, combinedMask);

        bool hitPlayer = false;
        HashSet<BaseEnemy> trappedEnemies = new HashSet<BaseEnemy>();

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                if (playerHealth == null)
                {
                    playerHealth = hit.GetComponentInParent<PlayerHealth>();
                }

                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage, poiseDamage, gameObject);
                    playerManager?.SetInCombat();
                    hitPlayer = true;
                }

                continue;
            }

            if (!trapEnemiesOnHit || enemyTrapDuration <= 0f) continue;

            BaseEnemy enemy = hit.GetComponentInParent<BaseEnemy>();
            if (enemy == null || enemy == this || enemy.IsDead()) continue;

            if (trappedEnemies.Add(enemy))
            {
                enemy.ApplyStun(enemyTrapDuration);
            }
        }

        return hitPlayer;
    }

    public virtual void OnAttackEnd()
    {
        isAttacking = false;
        isCharging = false;
        navAgent.updatePosition = true;
        navAgent.nextPosition = transform.position;
        FacePlayerImmediate();

        if (!isStunned && !isInHitStun && navAgent.isOnNavMesh)
        {
            navAgent.speed = chaseSpeed;
            navAgent.isStopped = false;
        }
    }

    public void ApplyPermanentBuff(float damageMultiplier, float attackSpeedMultiplier, GameObject buffVfxPrefab)
    {
        if (isBuffed)
        {
            Debug.Log($"{gameObject.name}: Buff ignored (already buffed).");
            return;
        }

        isBuffed = true;
        Debug.Log($"{gameObject.name}: Buff applied (damageMultiplier={damageMultiplier}, attackSpeedMultiplier={attackSpeedMultiplier}).");

        if (damageMultiplier > 0f)
        {
            lightAttackDamage *= damageMultiplier;
            heavyAttackDamage *= damageMultiplier;
            chargeAttackDamage *= damageMultiplier;
        }

        if (attackSpeedMultiplier > 0f)
        {
            attackCooldown = Mathf.Max(0.05f, attackCooldown / attackSpeedMultiplier);
        }

        if (buffVfxPrefab != null)
        {
            GameObject vfxInstance = Instantiate(buffVfxPrefab, transform);
            vfxInstance.transform.localPosition = Vector3.zero;
            vfxInstance.transform.localRotation = Quaternion.identity;
            Debug.Log($"{gameObject.name}: Buff VFX spawned ({buffVfxPrefab.name}).");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Buff VFX prefab is null.");
        }
    }

    public bool IsBuffed()
    {
        return isBuffed;
    }
    #endregion

    #region Stun
    public virtual void ApplyStun(float duration)
    {
        if (isStunned || isHitImmune || isCharging) return;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        isAttacking = false;
        isCharging = false;
        isInHitStun = false;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        yield return new WaitForSeconds(duration);

        isStunned = false;
        if (navAgent.isOnNavMesh) navAgent.isStopped = false;
    }

    protected virtual void EnterHitStun()
    {
        if (!isInHitStun)
        {
            isInHitStun = true;
            hitStunTimer = 0f;
            isHitImmune = true;
            hitImmuneTimer = hitImmunityDuration;
        }
        timeSinceLastHit = 0f;
    }

    protected virtual void ExitHitStunWithImmunity()
    {
        isInHitStun = false;
        hitStunTimer = 0f;
        timeSinceLastHit = 0f;
        navAgent.updatePosition = true;
        navAgent.nextPosition = transform.position;
        if (!isStunned && navAgent.isOnNavMesh) navAgent.isStopped = false;
    }
    #endregion

    // This lets you see the attack hitboxes in the Unity Editor when selecting the enemy
    private void OnDrawGizmosSelected()
    {
        DrawBoxGizmo("Light Attack Hitbox", lightAttackHitboxOffset, lightAttackHitboxSize, new Color(1, 0, 0, 0.3f));
        DrawBoxGizmo("Heavy Attack Hitbox", heavyAttackHitboxOffset, heavyAttackHitboxSize, new Color(1, 0.5f, 0, 0.3f));
        DrawBoxGizmo("Charge Attack Hitbox", chargeAttackHitboxOffset, chargeAttackHitboxSize, new Color(0.5f, 0, 1, 0.3f));
    }

    private void DrawBoxGizmo(string label, Vector3 offset, Vector3 size, Color color)
    {
        Gizmos.color = color;
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawCube(offset, size);
        Gizmos.DrawWireCube(offset, size);
    }

    public float GetHealthPercentage()
    {
        if (maxHealth <= 0f)
            return 0f;
        return Mathf.Clamp01(currentHealth / maxHealth);
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }

    #region StatusEffects
    public void SetSpeedMultiplier(float newMultiplier)
    {
        _currentSpeedMultiplier = newMultiplier;
        if (navAgent.isOnNavMesh && !isCharging) 
            navAgent.speed = chaseSpeed * _currentSpeedMultiplier;
            
        if (animator != null) 
            animator.speed = _currentSpeedMultiplier;
    }

    public void ResetSpeedMultiplier()
    {
        SetSpeedMultiplier(1f);
    }

    public void FreezeForHitstop()
    {
        // 1. Freeze the animation mid-frame
        if (animator != null) animator.speed = 0f;
        
        // 2. Safely halt the pathfinding without breaking their AI state
        if (navAgent.isOnNavMesh)
        {
            _storedNavSpeed = navAgent.speed;
            navAgent.speed = 0f;
            navAgent.velocity = Vector3.zero; // Stops any sliding!
        }
    }

    public void UnfreezeFromHitstop()
    {
        // 1. Unpause the animation
        if (animator != null) animator.speed = 1f;
        
        // 2. Give them their speed back so they continue exactly what they were doing
        if (navAgent.isOnNavMesh)
        {
            navAgent.speed = _storedNavSpeed;
        }
    }

    #endregion

}
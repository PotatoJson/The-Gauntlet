using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [Tooltip("How much health recovers per second after the delay.")]
    [SerializeField] private float healthRecoveryRate = 1f; // 1 HP per second = REAL slow

    [Tooltip("How long to wait after taking damage before health starts recovering.")]
    [SerializeField] private float healthRecoveryDelay = 5.0f; // 5 seconds of safety needed
    private float healthRecoveryTimer = 0f;

    [Header("Defense & Posture")]
    public bool isBlocking;
    [SerializeField] private float maxStunMeter = 100f;
    [SerializeField] private float stunRecoveryRate = 5f;
    [Tooltip("How long to wait after taking damage before posture starts recovering.")]
    [SerializeField] private float stunRecoveryDelay = 2.0f; // Wait 2 seconds
    private float stunRecoveryTimer = 0f; // Tracks the delay
    private float currentStunMeter = 0f;
    public bool isStunned { get; private set; }

    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float invincibilityTimer;
    private bool isInvincible;

    [Header("References")]
    private PlayerManager _stateManager;
    private Animator _animator;

    // Events for UI or other systems to subscribe to
    public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth
    public event Action OnPlayerDeath;

    public bool IsDead => currentHealth <= 0;
    public float HealthPercentage => currentHealth / maxHealth;

    public GameObject LastCheckPoint;

    private void Awake()
    {
        _stateManager = GetComponent<PlayerManager>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Update()
    {
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
            }
        }

        // gradually recover Stun Meter over time if not currently stunned
        if (currentStunMeter > 0 && !isStunned)
        {
            // tick down the delay timer first
            if (stunRecoveryTimer > 0)
            {
                stunRecoveryTimer -= Time.deltaTime;
            }
            // once the delay is over, slowly recover the meter
            else
            {
                currentStunMeter -= stunRecoveryRate * Time.deltaTime;
                currentStunMeter = Mathf.Max(0, currentStunMeter);
            }
        }

        if (currentHealth < maxHealth && !IsDead)
        {
            // tick down the delay timer first
            if (healthRecoveryTimer > 0)
            {
                healthRecoveryTimer -= Time.deltaTime;
            }
            // once the delay is over, slowly recover health
            else
            {
                currentHealth += healthRecoveryRate * Time.deltaTime;
                currentHealth = Mathf.Min(currentHealth, maxHealth); // Don't heal past Max HP

                // trigger events
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }

    public void TakeDamage(float damage, GameObject attacker = null)
    {
        if (IsDead || isInvincible) return;

        // ... Existing Parry/Block/Dodge logic would go here if uncommented ...

        currentHealth = Mathf.Max(currentHealth, 0);

        // Brief invincibility to prevent multiple hits from same attack
        
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        TriggerLargeStumble();
        //if (currentHealth <= 0)
        //{
        //    Die();
        //}
    }

    private void TriggerLargeStumble()
        {
            _stateManager.SetPlayerState(PlayerState.Staggered);
            _animator.SetTrigger("LargeStumble");
            _stateManager.CurrentLungeSpeed = -4f;
        }
    //private void BreakGuard()
    //{
    //    Debug.Log("GUARD BROKEN! Player is stunned.");
    //    isStunned = true;
    //
    //    // Reset the meter visually
    //    currentStunMeter = 0;
    //
    //    // Tell CombatSandBox to lock inputs and force drop the shield
    //    float stunDuration = 2.0f; // 2 seconds of stun
    //    _combatSandBox.TriggerGuardBreak(stunDuration);
    //
    //    // Tell PlayerHealth to unlock after the duration
    //    Invoke(nameof(RecoverFromStun), stunDuration);
    //}

    private void RecoverFromStun()
    {
        Debug.Log("Player recovered from stun.");
        isStunned = false;
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void PlayerRespawnSpikes()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false; // Turn it off so it stops "holding" the position

        transform.position = LastCheckPoint.transform.position;

        if (cc != null) cc.enabled = true; // Turn it back on
    }
}
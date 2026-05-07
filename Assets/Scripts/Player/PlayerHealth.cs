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

    [Header("Poise")]
    public int MaxPoise;
    public float CurrentPoise;
    //public Slider PoiseBar; //If we want to visualize the poise and stagger on the player
    public float PoiseRecoveryRate;
    public float PoiseRecoveryDelay;
    private float PoiseRecoveryTimer;

    [Header("Stagger things")]//these will be used to trigger a large knockback throwing the player
    public float InstantKnockback;
    public float OvercapKnockback;

    // Events for UI or other systems to subscribe to
    public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth
    public event Action<float, int> OnPoiseChanged; //CurrentPoise, MaxPoise
    public event Action OnPlayerDeath;

    public bool IsDead => currentHealth <= 0;
    public float HealthPercentage => currentHealth / maxHealth;

    public GameObject LastCheckPoint;
    public GameObject PlayerSpawn;

    private void Awake()
    {
        _stateManager = GetComponent<PlayerManager>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        CurrentPoise = 0;
        OnPoiseChanged?.Invoke(CurrentPoise, MaxPoise);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Update()
    {
        HandleInvincibility();
        HandlePoiseRecovery();
        HandleHealthRecovery();
    }

    #region UpdateClarity
    private void HandleInvincibility()
    {
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0) isInvincible = false;
        }
    }

    private void HandlePoiseRecovery()
    {
        if (CurrentPoise > 0)
        {
            if (PoiseRecoveryTimer > 0)
            {
                PoiseRecoveryTimer -= Time.deltaTime;
            }
            else
            {
                CurrentPoise -= PoiseRecoveryRate * Time.deltaTime;
                CurrentPoise = Mathf.Max(0, CurrentPoise);
                OnPoiseChanged?.Invoke(CurrentPoise, MaxPoise);
            }
        }
    }

    private void HandleHealthRecovery()
    {
        if (currentHealth < maxHealth && !IsDead)
        {
            if (healthRecoveryTimer > 0)
            {
                healthRecoveryTimer -= Time.deltaTime;
            }
            else
            {
                currentHealth += healthRecoveryRate * Time.deltaTime;
                currentHealth = Mathf.Min(currentHealth, maxHealth);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }
    #endregion

    public void TakeDamage(float damage/*, int poiseDamage*/, GameObject attacker = null)
    {
        Debug.Log("TakeDamage Test");
        if (IsDead || isInvincible) return;

        // ... Existing Parry/Block/Dodge logic would go here if uncommented ...
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        healthRecoveryTimer = healthRecoveryDelay;
        stunRecoveryTimer = stunRecoveryDelay;

        // Brief invincibility to prevent multiple hits from same attack
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log("TakeDamage Test " + currentHealth);
        if (currentHealth <= 0)
        {
            HandleDeath();
            return;
        }
        //HandleStagger(poiseDamage);
    }

    #region Stagger Handling 
    private void HandleStagger(int poiseDamage)
    {
        //see if player gets thrown from attack
        bool isKnockBack = poiseDamage >= InstantKnockback || (CurrentPoise + poiseDamage) >= (MaxPoise + OvercapKnockback);
        //check for large stagger for player stumble+staggered state
        bool isHeavyStagger = (CurrentPoise + poiseDamage) >= MaxPoise;

        if (isKnockBack)
        {
            CurrentPoise = 0;
            TriggerKnockback();
        }
        else if (isHeavyStagger)
        {
            CurrentPoise = 0;
            TriggerLargeStumble();
        }
        else
        {
            CurrentPoise += poiseDamage;
            TriggerSmallFlinch();
        }
    }

    private void TriggerKnockback()
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        _animator.SetTrigger("KnockbackHit");
        _stateManager.CurrentLungeSpeed = -20;
    }

    private void TriggerLargeStumble()
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        _animator.SetTrigger("LargeStumble");
        _stateManager.CurrentLungeSpeed = -4f;
    }

    private void TriggerSmallFlinch()
    {//this doesn't change state at all just some extra feedback for player
        _animator.SetTrigger("SmallFlinch");
    }

    #endregion

    private void HandleDeath()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = PlayerSpawn.transform.position;
        if (cc != null) cc.enabled = true;
        Debug.Log("Death Test");
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
        //if (IsDead) return;

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
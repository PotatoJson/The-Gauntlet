using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.VisualScripting;

public class PlayerHealth : MonoBehaviour
{
    private PlayerStatsManager _statsManager;

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    public float MaxHealth => maxHealth;
    private float _currentHealth;
    [Tooltip("How much health recovers per second after the delay.")]
    [SerializeField] private float healthRecoveryRate = 1f; // 1 HP per second = REAL slow

    [Tooltip("How long to wait after taking damage before health starts recovering.")]
    [SerializeField] private float healthRecoveryDelay = 5.0f; // 5 seconds of safety needed
    private float healthRecoveryTimer = 0f;

    [Header("Defense & Posture")]
    public bool isBlocking;

    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float invincibilityTimer;
    private bool isInvincible;

    [Header("References")]
    private PlayerManager _stateManager;
    private Animator _animator;

    [Header("Poise")]
    public float _maxPoise;
    public float CurrentPoise;
    //public Slider PoiseBar; //If we want to visualize the poise and stagger on the player
    public float PoiseRecoveryRate;
    public float PoiseRecoveryDelay;
    private float PoiseRecoveryTimer;

    [Header("Stagger stuff")]//these will be used to trigger a large knockback throwing the player
    public float InstantKnockback;
    public float OvercapKnockback;

    [Header("Potion Settings")]
    public int MaxPotions = 5;
    public int CurrentPotions;
    public float PotionHealAmount = 35f;
    public event Action<int, int> OnPotionCountChanged;

    [Header("VFX")]
    [Tooltip("Drag your healing VFX prefab here.")]
    [SerializeField] private GameObject healingVfxPrefab;
    [Tooltip("Optional: Drag a specific transform here (like the chest/feet). If empty, spawns at the player's base.")]
    [SerializeField] private Transform vfxSpawnPoint;

    [Header("Haptic Feedback")]
    [Tooltip("Low frequency motor (left side). Heavy, deep rumble.")]
    [Range(0f, 1f)] [SerializeField] private float damageRumbleLow = 0.5f;
    
    [Tooltip("High frequency motor (right side). Sharp, light vibration.")]
    [Range(0f, 1f)] [SerializeField] private float damageRumbleHigh = 0.8f;
    
    [Tooltip("How long the controller vibrates when taking damage.")]
    [SerializeField] private float damageRumbleDuration = 0.25f;

    // Events for UI or other systems to subscribe to
    public event Action<float, float> OnHealthChanged; // _currentHealth, maxHealth
    public event Action<float, float> OnPoiseChanged; //CurrentPoise, MaxPoise
    public event Action OnPlayerDeath;

    public bool IsDead => _currentHealth <= 0;
    public float HealthPercentage => _currentHealth / maxHealth;
    public float CurrentHealth => _currentHealth;

    public GameObject LastCheckPoint;
    public GameObject PlayerSpawn;

    [Header("Death")]
    [Tooltip("How fast time runs while a knockdown hit throws the player to their death.")]
    [Range(0.05f, 1f)] [SerializeField] private float knockdownSlowMotion = 0.3f;
    [Tooltip("How long that slow motion lasts, in real seconds.")]
    [SerializeField] private float knockdownSlowSeconds = 1.1f;

    private const string DeathState = "Death";
    private const string KnockdownDeathState = "DeathKnockdown";
    private const float KnockbackSpeed = -15f;
    private const float DeathCameraYaw = 150f;  // from the player's facing: in front, a little to the side
    private const float DeathCameraPitch = 25f; // looking down at the body
    private const float DeathHoldTrim = 0.3f; // the last moment of each clip is just the body lying still
    private Coroutine _dying;

    private void Awake()
    {
        _statsManager = GetComponent<PlayerStatsManager>();
        _stateManager = GetComponent<PlayerManager>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if(_statsManager != null)
        {
            _statsManager.OnStatsCalculated += HandleMaxHealthChange;  
            _statsManager.OnStatsCalculated += HandleMaxPoiseChange;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if(_statsManager != null)
        {
            _statsManager.OnStatsCalculated -= HandleMaxHealthChange;
            _statsManager.OnStatsCalculated -= HandleMaxPoiseChange;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }

    private void Start()
    {
        CurrentPotions = MaxPotions;
        _currentHealth = maxHealth;
        CurrentPoise = 0;
        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnPotionCountChanged?.Invoke(CurrentPotions, MaxPotions);
    }

    private void Update()
    {
        HandleInvincibility();
        HandlePoiseRecovery();
        HandleHealthRecovery();
    }

#region StatChangesFromGems
    private void HandleMaxPoiseChange()
    {
        _maxPoise = _statsManager.CurrentMaxPoise;
        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
    }

    private void HandleMaxHealthChange()
    {
        //Debug.Log("Pre Health gem - " + maxHealth);
        float newMax = _statsManager.CurrentMaxHealth;
        float differnece = newMax - maxHealth;

        maxHealth = newMax;

        if(differnece > 0) _currentHealth += differnece;
        else _currentHealth = Mathf.Min(_currentHealth, maxHealth);
        Debug.Log("Post Health gem - " + maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }
#endregion

#region PotionHandling
    public bool TryConsumePotion()
    {
        if(CurrentPotions > 0 && _currentHealth < maxHealth)
        {
            CurrentPotions--;

            OnPotionCountChanged?.Invoke(CurrentPotions, MaxPotions);

            return true;
        }
        return false;
    }

    public void AddPotion()
    {
        // Only add a potion if we aren't already at the maximum limit!
        if (CurrentPotions < MaxPotions)
        {
            CurrentPotions++;

            // Tell the UI to draw a new potion bottle!
            OnPotionCountChanged?.Invoke(CurrentPotions, MaxPotions);

            Debug.Log($"Debug: Added 1 Potion! Total: {CurrentPotions}");
        }
        else
        {
            Debug.Log("Debug: Potion count is already at max!");
        }
    }

    public void ExecutePotionHeal()
    {
        Heal(PotionHealAmount);
        Debug.Log($"Healed Potions remaining: {CurrentPotions}");

        _stateManager.SetInCombat();

        if (healingVfxPrefab != null)
        {
            // Figure out where to spawn it (use the custom point, or just the player's position)
            Transform spawnLocation = (vfxSpawnPoint != null) ? vfxSpawnPoint : transform;

            // Spawn the prefab as a child of the player so it moves WITH the player!
            GameObject spawnedVFX = Instantiate(healingVfxPrefab, spawnLocation.position, spawnLocation.rotation, transform);
        }

    }
#endregion

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
                OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
            }
        }
    }

    private void HandleHealthRecovery()
    {
        if (_currentHealth < maxHealth && !IsDead)
        {
            if (healthRecoveryTimer > 0)
            {
                healthRecoveryTimer -= Time.deltaTime;
            }
            else
            {
                _currentHealth += healthRecoveryRate * Time.deltaTime;
                _currentHealth = Mathf.Min(_currentHealth, maxHealth);
                OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            }
        }
    }
    #endregion

    public void TakeDamage(float damage, int poiseDamage = 0, GameObject attacker = null)
    {
        Debug.Log("TakeDamage Test");
        if (IsDead || isInvincible) return;

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null && movement.IsInvincibleViaRoll)
        {
            Debug.Log("Dodged the attack!");
            return; // Completely ignore the damage!
        }

        // ... Existing Parry/Block/Dodge logic would go here if uncommented ...
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(_currentHealth, 0);

        healthRecoveryTimer = healthRecoveryDelay;

        // Brief invincibility to prevent multiple hits from same attack
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;

        TriggerHaptics(damageRumbleLow, damageRumbleHigh, damageRumbleDuration);
        TriggerHitFeedback(damage);
        if (_currentHealth > 0f) FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Hurt");

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        Debug.Log("TakeDamage Test " + _currentHealth);

        HitFeel.PlayerHit(damage, attacker);
        if (_currentHealth <= 0)
        {
            RecordDeathAndDie(attacker, poiseDamage > 0 && IsKnockback(poiseDamage));
            return;
        }

        if (poiseDamage > 0)
        {
            HandleStagger(poiseDamage, attacker);
        }
    }

    /// <summary>Instant death from a hazard: invincibility and roll frames don't save the player.</summary>
    public void Kill(GameObject killer)
    {
        if (IsDead) return;

        _currentHealth = 0f;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        RecordDeathAndDie(killer, false);
    }

    private void RecordDeathAndDie(GameObject attacker, bool knockedDown)
    {
        if (MetricsTracker.Instance != null)
        {
            // Fallback to "Unknown" if the attacker wasn't passed or was a trap
            string killerName = attacker != null ? attacker.name : "Unknown/Environment";

            // Clean up clone tags in the name (e.g., "BasicEnemy(Clone)" -> "BasicEnemy")
            killerName = killerName.Replace("(Clone)", "").Trim();

            MetricsTracker.Instance.EndChamber(Mathf.RoundToInt(_currentHealth), true, killerName);
        }
        HandleDeath(attacker, knockedDown);
    }

    #region Stagger Handling
    // An Earth gauntlet's passive gem: basic attacks shrug off hits, heavy attacks are easier to break.
    private int AdjustedPoiseDamage(int poiseDamage)
    {
        PlayerCombat combat = GetComponent<PlayerCombat>();
        return combat != null ? Mathf.Max(1, Mathf.RoundToInt(poiseDamage * combat.IncomingPoiseMultiplier())) : poiseDamage;
    }

    // Whether a hit this strong throws the player down, rather than making them stumble or flinch.
    private bool IsKnockback(int poiseDamage) => AdjustedPoiseDamage(poiseDamage) >= InstantKnockback;

    private void HandleStagger(int poiseDamage, GameObject attacker)
    {
        PoiseRecoveryTimer = PoiseRecoveryDelay;
        poiseDamage = AdjustedPoiseDamage(poiseDamage);

        //see if player gets thrown from attack
        Debug.Log($"[POISE] Took {poiseDamage} poise damage! (Poise before hit: {CurrentPoise}/{_maxPoise})");

        bool isKnockBack = poiseDamage >= InstantKnockback/* || (CurrentPoise + poiseDamage) >= (_maxPoise + OvercapKnockback)*/;
        bool isHeavyStagger = (CurrentPoise + poiseDamage) >= _maxPoise;

        if (isKnockBack)
        {
            CurrentPoise = 0;
            Debug.Log("[POISE] BREAK! Knockback triggered. Poise reset to 0.");
            TriggerKnockback(attacker);
        }
        else if (isHeavyStagger)
        {
            CurrentPoise = 0;
            Debug.Log("[POISE] BREAK! Large Stumble triggered. Poise reset to 0.");
            TriggerLargeStumble(attacker);
        }
        else
        {
            CurrentPoise += poiseDamage;
            Debug.Log($"[POISE] Absorbed hit (Small Flinch). Poise is now {CurrentPoise}/{_maxPoise}");
            TriggerSmallFlinch();
        }

        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
    }

    private void TriggerKnockback(GameObject attacker)
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        FaceAttacker(attacker);

        _animator.Play("Null", 1); // clears a hit reaction still playing on the HitReactions layer
        _animator.SetTrigger("KnockbackHit");
        _stateManager.CurrentLungeSpeed = KnockbackSpeed;
    }

    private void FaceAttacker(GameObject attacker)
    {
        if (attacker != null)
        {
            Vector3 directionToAttacker = attacker.transform.position - transform.position;
            directionToAttacker.y = 0; // Keep rotation flat
            
            if (directionToAttacker.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(directionToAttacker.normalized);
            }
        }
    }

    private void TriggerLargeStumble(GameObject attacker)
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        FaceAttacker(attacker);

        _animator.Play("Null", 1); // clears a hit reaction still playing on the HitReactions layer
        _animator.SetTrigger("LargeStumble");
        _stateManager.CurrentLungeSpeed = -4f;
    }

    private void TriggerSmallFlinch()
    {//this doesn't change state at all just some extra feedback for player
        _animator.SetTrigger("SmallFlinch");
    }

    #endregion

    private void HandleDeath(GameObject killer, bool knockedDown)
    {
        // 0. The run is over, so the checkpoint save must go. Leaving it behind would let the
        //    player relaunch and resume from before they died.
        if (SaveManager.Instance != null) SaveManager.Instance.DeleteSave();

        FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Death");

        // 1. No more input from here on: no moving, attacking or opening the pause menu over the death.
        //    Loading a level (restart or main menu) gives it back.
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);
        _stateManager.SetPlayerState(PlayerState.Staggered); // combat drops anything buffered or charging
        OnPlayerDeath?.Invoke();

        // 2. The death animation, then the screen falling apart and the Game Over menu.
        if (_dying != null) StopCoroutine(_dying);
        _dying = StartCoroutine(DeathSequence(killer, knockedDown));

        Debug.Log("Player died. Awaiting Restart...");
    }

    private IEnumerator DeathSequence(GameObject killer, bool knockedDown)
    {
        _animator.speed = 1f;
        _animator.Play("Null", 1); // clears a hit reaction still playing on the HitReactions layer

        if (knockedDown)
        {
            // Thrown back off their feet. The fall plays in slow motion, then lands at normal speed.
            FaceAttacker(killer);
            _stateManager.CurrentLungeSpeed = KnockbackSpeed;
            _animator.CrossFadeInFixedTime(KnockdownDeathState, 0.08f);

            // The attacker is usually in front, so the fall lands under the camera. Swing it round
            // in front of the player instead, looking back at them as they go down.
            PlayerCamera cam = PlayerCamera.Instance;
            if (cam != null) cam.ClearLockOnTarget();
            float fromYaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            float toYaw = transform.eulerAngles.y + DeathCameraYaw;

            for (float elapsed = 0f; elapsed < knockdownSlowSeconds; elapsed += Time.unscaledDeltaTime)
            {
                Time.timeScale = knockdownSlowMotion; // every frame: a hit-stop ending would put it back to 1
                float e = Mathf.SmoothStep(0f, 1f, elapsed / knockdownSlowSeconds);
                if (cam != null) cam.SetLookAngles(Mathf.LerpAngle(fromYaw, toYaw, e), DeathCameraPitch * e);
                yield return null;
            }
            Time.timeScale = 1f;
        }
        else
        {
            _stateManager.CurrentLungeSpeed = 0f;
            _animator.CrossFadeInFixedTime(DeathState, 0.15f);
        }

        // Let the clip play out on the floor before the screen breaks. The cross-fade can take a few
        // frames to start (a hit-stop slows them to almost nothing), so wait until the clip is really playing.
        string deathState = knockedDown ? KnockdownDeathState : DeathState;
        while (!_animator.GetCurrentAnimatorStateInfo(0).IsName(deathState)) yield return null;
        AnimatorStateInfo death = _animator.GetCurrentAnimatorStateInfo(0);
        float remaining = death.length * (1f - death.normalizedTime);
        yield return new WaitForSeconds(Mathf.Max(0f, remaining - DeathHoldTrim));

        // Frozen where they fell.
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        _dying = null;
        DeathScreenShatter.Instance.TriggerDeathShatter(killer);
    }

    // A restart reloads the level but keeps this player, so take them out of the death pose.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_dying != null)
        {
            StopCoroutine(_dying);
            _dying = null;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName(DeathState) || state.IsName(KnockdownDeathState))
        {
            _animator.Play("Locomotion", 0, 0f);
            _stateManager.SetPlayerState(PlayerState.Idle);
            _stateManager.CurrentLungeSpeed = 0f;
        }
    }

    public void Heal(float amount)
    {
        //if (IsDead) return;
        _currentHealth += amount;
        _currentHealth = Mathf.Min(_currentHealth, maxHealth);

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// Puts health, poise and potions back to a saved snapshot. Called by SaveManager AFTER
    /// the gems have been re-equipped, so maxHealth already includes any gem bonuses.
    /// </summary>
    public void RestoreState(float health, float poise, int potions)
    {
        _currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        CurrentPoise = _maxPoise > 0f ? Mathf.Clamp(poise, 0f, _maxPoise) : 0f;
        CurrentPotions = Mathf.Clamp(potions, 0, MaxPotions);

        // Don't let the slow out-of-combat regen start the instant the player loads in.
        healthRecoveryTimer = healthRecoveryDelay;

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
        OnPotionCountChanged?.Invoke(CurrentPotions, MaxPotions);
    }

    /// <summary>Moves the player without the CharacterController fighting the teleport.</summary>
    public void TeleportTo(Vector3 position, float yaw)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cc != null) cc.enabled = true;

        if (PlayerCamera.Instance != null) PlayerCamera.Instance.SnapToTarget();
    }

    public void PlayerRespawnSpikes()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false; // Turn it off so it stops "holding" the position

        transform.position = LastCheckPoint.transform.position;

        if (cc != null) cc.enabled = true; // Turn it back on

        if (PlayerCamera.Instance != null) PlayerCamera.Instance.SnapToTarget();
    }

    public void TriggerHitFeedback(float damageAmount)
    {
        // Trigger your hit animation
        GetComponentInChildren<Animator>().SetTrigger("HitReaction");

        // Only do Hitstop for big attacks so it doesn't get annoying
        if (damageAmount > 15f) 
        {
            StartCoroutine(HitstopRoutine(0.1f)); // Freeze for 1/10th of a second
        }
    }

    private System.Collections.IEnumerator HitstopRoutine(float duration)
    {
        // Freeze time!
        Time.timeScale = 0.05f; 
        
        // Wait in real-time so the freeze actually ends
        yield return new WaitForSecondsRealtime(duration); 
        
        // Return to normal speed
        Time.timeScale = 1f; 
    }

    #region Haptics
    private void TriggerHaptics(float lowFrequency, float highFrequency, float duration)
    {
        // Check if there is an active gamepad connected
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            // Start the motors
            gamepad.SetMotorSpeeds(lowFrequency, highFrequency);
            
            // Start a coroutine to stop the motors after 'duration'
            StartCoroutine(StopHapticsCoroutine(gamepad, duration));
        }
    }

    private IEnumerator StopHapticsCoroutine(Gamepad gamepad, float duration)
    {
        // Using WaitForSecondsRealtime ensures the rumble stops even if Time.timeScale == 0 (e.g., hit pause or pausing the game)
        yield return new WaitForSecondsRealtime(duration);

        // Double check if the gamepad is still connected/current before stopping
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }
    #endregion

}
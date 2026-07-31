using UnityEngine;
using System;

public class PlayerStamina : MonoBehaviour
{
    private PlayerStatsManager _statsManager;

    /*[Header("Stamina Settings")]
    public float RegenRate = 60f;
    public float RegenDelay = 3f;*/
    
    private float _currentStamina;
    private float _maxStamina;
    private float _regenTimer = 0f;

    [SerializeField] private AnimatedStaminaBar _staminaBar;

    public event Action<float, float> OnStaminaChanged;

    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;

    private void Awake()
    {
        _statsManager = GetComponent<PlayerStatsManager>();
    }

    private void OnEnable()
    {
        if (_statsManager != null) _statsManager.OnStatsCalculated += HandleMaxStaminaChange;
    }

    private void OnDisable()
    {
        if (_statsManager != null) _statsManager.OnStatsCalculated -= HandleMaxStaminaChange;
    }

    private void Start()
    {
        _maxStamina = _statsManager.CurrentStamina; // Pull the initial max from the manager
        _currentStamina = _maxStamina;
        UpdateUI();
    }

    private void Update()
    {
        if (_currentStamina < _maxStamina)
        {
            if (_regenTimer > 0)
            {
                _regenTimer -= Time.deltaTime;
            }
            else if(_statsManager != null)
            {
                _currentStamina += _statsManager.CurrentStaminaRegen * Time.deltaTime;
                _currentStamina = Mathf.Min(_currentStamina, _maxStamina);
                UpdateUI();
            }
        }
    }

    //used by gem effects manager
    public void RestoreStamina(float amount)
    {
        _currentStamina += amount;
        
        // Clamp to max so it doesn't overflow
        _currentStamina = Mathf.Min(_currentStamina, _maxStamina);
        
        // Force the UI bar to update instantly
        UpdateUI(); 
    }

    /// <summary>
    /// Puts the stamina/mana bar back to a saved snapshot. Called by SaveManager after the
    /// gems are re-equipped so the max is already correct.
    /// </summary>
    public void RestoreState(float stamina)
    {
        if (_statsManager != null) _maxStamina = _statsManager.CurrentStamina;

        _currentStamina = Mathf.Clamp(stamina, 0f, _maxStamina);
        _regenTimer = 0f;
        UpdateUI();
    }

    private void HandleMaxStaminaChange()
    {
        //Debug.Log("Pre gem stamina - " + _maxStamina);
        _maxStamina = _statsManager.CurrentStamina;
        _currentStamina = Mathf.Min(_currentStamina, _maxStamina);
        Debug.Log("Post gem stamina - " + _maxStamina);
        UpdateUI();
    }

    // used by combat and movement
    public bool HasEnoughStamina(float cost)
    {
        return _currentStamina > 0f;
    }

    public void ConsumeStamina(float amount)
    {
        _currentStamina -= amount;
        // Metrics: Check if stamina just bottomed out
        if (_currentStamina <= 0 && MetricsTracker.Instance != null)
        {
            MetricsTracker.Instance.RecordStaminaExhaustion();
        }
        _currentStamina = Mathf.Max(0, _currentStamina);
        _regenTimer = _statsManager.CurrentStaminaDelay; // Reset the delay before it starts regenerating
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_staminaBar != null)
        {
            _staminaBar.SetStamina(Mathf.RoundToInt(_currentStamina), Mathf.RoundToInt(_maxStamina));
        }
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }
}
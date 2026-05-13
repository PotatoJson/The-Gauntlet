using UnityEngine;
using System;

public class PlayerStamina : MonoBehaviour
{
    private PlayerStatsManager _statsManager;

    [Header("Stamina Settings")]
    public float RegenRate = 60f;
    public float RegenDelay = 3f;
    
    private float _currentStamina;
    private float _maxStamina;
    private float _regenTimer = 0f;

    [SerializeField] private StaminaBar _staminaBar;

    public event Action<float, float> OnStaminaChanged;

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
            else
            {
                _currentStamina += RegenRate * Time.deltaTime;
                _currentStamina = Mathf.Min(_currentStamina, _maxStamina);
                UpdateUI();
            }
        }
    }

    private void HandleMaxStaminaChange()
    {
        Debug.Log("Pre gem stamina - " + _maxStamina);
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
        _regenTimer = RegenDelay; // Reset the delay before it starts regenerating
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
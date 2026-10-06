using UnityEngine;
using System;

/// <summary>
/// Mana pool spent by skill casts (Q/E). Regenerates at a steady rate with no delay.
/// Max and regen come from PlayerStatsManager so gems can modify them.
/// </summary>
public class PlayerMana : MonoBehaviour
{
    [Tooltip("Mana every skill costs. Same for all skills; the gauntlet's element is what differs.")]
    [SerializeField] private float _skillCost = 20f;

    private PlayerStatsManager _statsManager;
    private AnimatedManaBar _manaBar;

    private float _currentMana;
    private float _maxMana;

    public event Action<float, float> OnManaChanged;

    public float CurrentMana => _currentMana;
    public float MaxMana => _maxMana;
    public float SkillCost => _skillCost;

    private void Awake()
    {
        _statsManager = GetComponent<PlayerStatsManager>();
    }

    private void OnEnable()
    {
        if (_statsManager != null) _statsManager.OnStatsCalculated += HandleMaxManaChange;
    }

    private void OnDisable()
    {
        if (_statsManager != null) _statsManager.OnStatsCalculated -= HandleMaxManaChange;
    }

    private void Start()
    {
        // The HUD lives in a separate prefab, so find the bar at runtime instead of wiring it per scene.
        _manaBar = FindFirstObjectByType<AnimatedManaBar>(FindObjectsInactive.Include);
        _maxMana = _statsManager.CurrentMana;
        _currentMana = _maxMana;
        UpdateUI();
    }

    private void Update()
    {
        if (_currentMana >= _maxMana) return;

        _currentMana = Mathf.Min(_currentMana + _statsManager.CurrentManaRegen * Time.deltaTime, _maxMana);
        UpdateUI();
    }

    // Skills need the full cost; a partial pool can't cast.
    public bool HasEnoughMana(float cost) => _currentMana >= cost;

    public void ConsumeMana(float amount)
    {
        _currentMana = Mathf.Max(0f, _currentMana - amount);
        UpdateUI();
    }

    public void RestoreMana(float amount)
    {
        _currentMana = Mathf.Min(_currentMana + amount, _maxMana);
        UpdateUI();
    }

    /// <summary>Called by SaveManager after gems are re-equipped, so the max is already correct. Negative = full.</summary>
    public void RestoreState(float mana)
    {
        if (_statsManager != null) _maxMana = _statsManager.CurrentMana;

        _currentMana = mana < 0f ? _maxMana : Mathf.Clamp(mana, 0f, _maxMana);
        UpdateUI();
    }

    private void HandleMaxManaChange()
    {
        // A bigger max (gem equipped, or stats not ready at Start) grants the difference; a smaller one clamps.
        float newMax = _statsManager.CurrentMana;
        _currentMana = Mathf.Clamp(_currentMana + Mathf.Max(0f, newMax - _maxMana), 0f, newMax);
        _maxMana = newMax;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_manaBar != null) _manaBar.SetStamina(Mathf.RoundToInt(_currentMana), Mathf.RoundToInt(_maxMana));
        OnManaChanged?.Invoke(_currentMana, _maxMana);
    }
}

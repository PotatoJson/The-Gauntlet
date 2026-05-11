using UnityEngine;
using System.Collections.Generic;

public class PlayerStatsManager : MonoBehaviour
{
    [Header("Base Stats")]
    public float BaseMaxHealth = 100f;
    public float BaseDamage = 10f;

    [Header("Current Global Stats")]
    public float CurrentMaxHealth;
    public float CurrentDamage;

    public RunTimeGauntlet PrimaryGauntlet;
    public RunTimeGauntlet SecondaryGauntlet;

    //safe gaurd that holds the sum of all active bonuses making sure there are no duplicating stats
    private Dictionary<StatModifierType, float> _activeBonuses = new Dictionary<StatModifierType, float>();

    //probably link players scripts here, but maybe go through player manager
    [SerializeField] private PlayerHealth _healthScript;

    private void Start()
    {
        RecalculateGlobalStats();
    }

    public void SyncWithUI(Transform primaryUIParent, Transform secondaryUIParent)
    {
        PrimaryGauntlet = BuildGauntletFromUI(primaryUIParent, EquipSlot.Primary);
        SecondaryGauntlet = BuildGauntletFromUI(secondaryUIParent, EquipSlot.Secondary);
        RecalculateGlobalStats();
    }

    private RunTimeGauntlet BuildGauntletFromUI(Transform uiParent, EquipSlot slotContext)
    {
        GauntletManager gManager = uiParent.GetComponentInChildren<GauntletManager>();
        if(gManager == null || gManager.LinkedGauntletData == null) return null;

        //create the actual gauntlet from data
        RunTimeGauntlet newGauntlet = new RunTimeGauntlet(gManager.LinkedGauntletData, slotContext);

        for (int i = 0; i < gManager.currentActiveSlots; i++)
        {
            Transform slot = gManager.fingerSlots[i].transform;
            DraggableGem gemUI = slot.GetComponentInChildren<DraggableGem>();
            //check for valid gem
            if(gemUI != null && gemUI.LinkedGemData is StatGemData statGem)
            {
                newGauntlet.SocketedStatGems[i] = statGem;
            }
        }
        return newGauntlet;
    }

    public void RecalculateGlobalStats()
    {
        _activeBonuses.Clear();

        //get all modifiers
        List<GemModifier> allMods = new List<GemModifier>();
        if(PrimaryGauntlet != null) allMods.AddRange(PrimaryGauntlet.GetAllActiveModifiers());
        if(SecondaryGauntlet != null) allMods.AddRange(SecondaryGauntlet.GetAllActiveModifiers());
        Debug.Log($"Total active modifiers found across both hands: {allMods.Count}");
        foreach(GemModifier mod in allMods)
        {
            if(_activeBonuses.ContainsKey(mod.StatType)) _activeBonuses[mod.StatType] += mod.Amount;
            else
            {
                _activeBonuses.Add(mod.StatType, mod.Amount);
            }
        }

        float primaryWeaponDamage = 0f;
        if(PrimaryGauntlet != null && PrimaryGauntlet.BaseGauntlet != null)
        {
            primaryWeaponDamage = PrimaryGauntlet.BaseGauntlet.Damage;
        }

        CurrentMaxHealth = BaseMaxHealth + GetBonus(StatModifierType.MaxHealth);
        CurrentDamage = BaseDamage + primaryWeaponDamage + GetBonus(StatModifierType.PhysicalDamage);
        Debug.Log("Current Damage " + CurrentDamage);
        //maybe update player health bar max?
        _healthScript.UpdateMaxHealth(CurrentMaxHealth);
    }

    //this is a universal function that other scripts will need to call in order to know what 
    //needs to change on their end
    public float GetBonus(StatModifierType statType)
    {
        if (_activeBonuses.ContainsKey(statType))
        {
            return _activeBonuses[statType];
        }
        return 0f;
    }
}

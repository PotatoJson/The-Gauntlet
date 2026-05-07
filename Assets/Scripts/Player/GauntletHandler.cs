using UnityEngine;
using System.Collections.Generic;

public class RunTimeGauntlet
{
    public GauntletData BaseGauntlet;
    public EquipSlot CurrentSlot = EquipSlot.None;
    //gem sockets
    public StatGemData[] SocketedStatGems; 
    public SkillGemData SockedSkill; 

    public RunTimeGauntlet(GauntletData gauntletData, EquipSlot initialSlot)
    {
        BaseGauntlet = gauntletData;
        CurrentSlot = initialSlot;

        //always creating an array based on the max number of slots to prevent gems from being destroyed
        //if we allow the player to swap gauntlets back and forth.
        SocketedStatGems = new StatGemData[BaseGauntlet.MaxPrimaryGemSlots];
    }

    //Hand Restriction logic
    public List<StatGemData> GetActiveStatGems()
    {
        List<StatGemData> activeGems = new List<StatGemData>();

        //secondary hand gets reduced gem slots
        int activeSlotCount = (CurrentSlot == EquipSlot.Primary)
            ? BaseGauntlet.MaxPrimaryGemSlots
            : BaseGauntlet.MaxSecondaryGemSlots;

        for(int i = 0; i < activeSlotCount; i++)
        {
            if(SocketedStatGems[i] != null) activeGems.Add(SocketedStatGems[i]);
        }
        return activeGems;
    }

    public float GetTotalStatBonus(StatModifierType statType)
    {
        float totalBonus = 0f;
        foreach(StatGemData gem in GetActiveStatGems())
        {
            foreach(GemModifier mod in gem.Modifiers)
            {
                if(mod.StatType == statType) totalBonus += mod.Amount;
            }
        }

        if(CurrentSlot == EquipSlot.Primary)
        {
            foreach(GemModifier mod in BaseGauntlet.InherentPassives)
            {
                if(mod.StatType == statType) totalBonus += mod.Amount;
            }
        }
        return totalBonus;
    }

    public int GetCurrentDamage()
    {
        float gemBonus = GetTotalStatBonus(StatModifierType.PhysicalDamage);
        return BaseGauntlet.Damage + Mathf.RoundToInt(gemBonus);
    }
    public int GetCurrentPoise()
    {
        float gemBonus = GetTotalStatBonus(StatModifierType.PoiseDamage);
        return BaseGauntlet.PoiseDamage + Mathf.RoundToInt(gemBonus);
    }

    //This will be used by UI when swapping gauntlets
    public StatGemData[] ExtractAllGems()
    {
        StatGemData[] extractedGems = (StatGemData[])SocketedStatGems.Clone();
        //Clear current gauntlet's gems
        for(int i = 0; i < SocketedStatGems.Length; i++)
        {
            SocketedStatGems[i] = null;
        }
        return extractedGems;
    }
}

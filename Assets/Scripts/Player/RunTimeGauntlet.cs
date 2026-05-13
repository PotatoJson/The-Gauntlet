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
        SocketedStatGems = new StatGemData[5];
    }

    //Hand gem slot Restriction logic
    public List<StatGemData> GetActiveStatGems()
    {
        List<StatGemData> activeGems = new List<StatGemData>();

        for(int i = 0; i < SocketedStatGems.Length; i++)
        {
            if(SocketedStatGems[i] != null) activeGems.Add(SocketedStatGems[i]);
        }
        return activeGems;
    }

    public List<GemModifier> GetAllActiveModifiers()
    {
        List<GemModifier> allMods = new List<GemModifier>();
        
        foreach(StatGemData gem in GetActiveStatGems())
        {
            allMods.AddRange(gem.Modifiers);
        }

        if(CurrentSlot == EquipSlot.Primary && BaseGauntlet.InherentPassives != null)
        {
            allMods.AddRange(BaseGauntlet.InherentPassives);
        }
        return allMods;
    }

    /*public int GetCurrentDamage()
    {
        float gemBonus = GetTotalStatBonus(StatModifierType.PhysicalDamage);
        return BaseGauntlet.Damage + Mathf.RoundToInt(gemBonus);
    }
    public int GetCurrentPoise()
    {
        float gemBonus = GetTotalStatBonus(StatModifierType.PoiseDamage);
        return BaseGauntlet.PoiseDamage + Mathf.RoundToInt(gemBonus);
    }*/

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

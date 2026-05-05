using UnityEngine;

public class RunTimeGauntlet
{
    public GauntletData BaseGauntlet;
    public GemData[] SocketedGems;

    public RunTimeGauntlet(GauntletData gauntletData)
    {
        BaseGauntlet = gauntletData;
        SocketedGems = new GemData[BaseGauntlet.MaxGemSlots];
    }

    public float GetTotalGemBonus(StatModifierType statType)
    {
        float totalBonus = 0f;
        foreach(GemData gem in SocketedGems)
        {
            if(gem == null) continue;

            foreach(GemModifier mod in gem.Modifiers)
            {
                if(mod.StatType == statType)
                {
                    totalBonus += mod.Amount;
                }
            }
        }
        return totalBonus;
    }

    public int GetCurrentDamage()
    {
        float gemBonus = GetTotalGemBonus(StatModifierType.PhysicalDamage);
        //eventually loop through gems to see if any increase damage
        return BaseGauntlet.Damage + Mathf.RoundToInt(gemBonus);
    }
    public int GetCurrentPoise()
    {
        float gemBonus = GetTotalGemBonus(StatModifierType.PoiseDamage);
        //loop through gems to get total poise based on gems later
        return BaseGauntlet.PoiseDamage + Mathf.RoundToInt(gemBonus);      
    }
}

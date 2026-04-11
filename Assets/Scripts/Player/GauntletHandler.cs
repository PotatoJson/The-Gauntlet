using UnityEngine;

public class RunTimeGauntlet
{
    public GauntletData BaseGauntlet;
    public object[] SocketedGems;

    public RunTimeGauntlet(GauntletData gauntletData)
    {
        BaseGauntlet = gauntletData;
        SocketedGems = new object[BaseGauntlet.MaxGemSlots];
    }

    public int GetCurrentDamage()
    {
        int totalDamage = BaseGauntlet.Damage;
        //eventually loop through gems to see if any increase damage
        return totalDamage;
    }
    public int GetCurrentPoise()
    {
        int totalPoise = BaseGauntlet.PoiseDamage;
        //loop through gems to get total poise based on gems later
        return totalPoise;      
    }
}

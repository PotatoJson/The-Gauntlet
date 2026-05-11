using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Stat Gem", menuName = "Items/Gems/Stat Gem")]
public class StatGemData : GemData
{
    [Header("Stat Bonuses")]
    public List<GemModifier> Modifiers = new List<GemModifier>();

    [Header("Cursed Properties")]
    public bool IsCursed;
    //add more logic here when we have more info on curses
}

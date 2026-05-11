using UnityEngine;
using System.Collections.Generic;

public abstract class GemData : BaseItemData
{
  [Header("Gem Properties")]
  public int GemTier;
  //potential socketing sound ADD HERE
}

[CreateAssetMenu(fileName = "New Stat Gem", menuName = "Items/Gems/Stat Gem")]
public class StatGemData : GemData
{
    [Header("Stat Bonuses")]
    public List<GemModifier> Modifiers = new List<GemModifier>();

    [Header("Cursed Properties")]
    public bool IsCursed;
    //add more logic here when we have more info on curses
}

[CreateAssetMenu(fileName = "New Skill Gem", menuName = "Items/Gems/Skill Gem")]
public class SkillGemData : GemData
{
    [Header("Skill Properties")]
    public GameObject SkillPrefab;
    public float BaseCooldown;
    public float StaminaCost;

    //example of general skill logic (see definitions script for list of categories)
    public SkillCategory Category;
}

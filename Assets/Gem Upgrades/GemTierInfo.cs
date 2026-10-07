using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// How a gem tier looks and reads: its quality name, colour, roman numeral and the effect it has on skill
/// gems. All the text comes from the Gem string table under keys prefixed "GemTier_", so it never touches
/// a gem's own title or description entries and is translated like the rest of the UI.
/// </summary>
public static class GemTierInfo
{
    public const string TableName = "Gem";

    // GemTier_Format          "Tier {0} - {1}"  ({0} = tier number, {1} = quality name)
    // GemTier_Name_1 .. _5    quality names
    private static readonly LocalizedString Format = new LocalizedString(TableName, "GemTier_Format");

    private static readonly string[] Roman = { "I", "II", "III", "IV", "V" };

    // Dark enough to read on the paper-white description panel, bright enough to glow as a gem outline.
    private static readonly Color[] Colors =
    {
        new Color(0.45f, 0.40f, 0.35f), // 1 rough
        new Color(0.15f, 0.60f, 0.25f), // 2 polished
        new Color(0.15f, 0.45f, 0.95f), // 3 flawless
        new Color(0.60f, 0.25f, 0.90f), // 4 radiant
        new Color(0.95f, 0.60f, 0.05f), // 5 perfect
    };

    private static int Index(int tier) => Mathf.Clamp(tier, 1, DraggableGem.MaxTier) - 1;

    public static Color TierColor(int tier) => Colors[Index(tier)];

    public static string RomanNumeral(int tier) => Roman[Index(tier)];

    /// <summary>"Tier 2 - Polished", in the current language.</summary>
    public static string Label(int tier)
    {
        int t = Index(tier) + 1;
        string quality = new LocalizedString(TableName, "GemTier_Name_" + t).GetLocalizedString();
        return string.Format(Format.GetLocalizedString(), t, quality);
    }

    /// <summary>Skill gems have no stat modifiers to scale, so a higher tier hits harder instead: +50% per tier above 1.</summary>
    public static float SkillDamageMultiplier(int tier) => 1f + 0.5f * (Index(tier));
}

using UnityEngine;

/// <summary>
/// Gem tier merging, ready for a crafting UI to call: two stat gems of the same kind and the same
/// tier become one gem a tier higher (Tier 1 + Tier 1 = Tier 2, Tier 2 + Tier 2 = Tier 3, ...).
/// </summary>
public static class GemCrafting
{
    public static bool CanMerge(DraggableGem a, DraggableGem b)
    {
        return a != null && b != null && a != b
            && a.LinkedGemData != null && a.LinkedGemData == b.LinkedGemData
            && a.LinkedGemData.gemType == GemType.Stat
            && a.Tier == b.Tier
            && a.Tier < DraggableGem.MaxTier;
    }

    /// <summary>
    /// Raises <paramref name="keep"/> one tier and destroys <paramref name="consume"/>.
    /// Equipped gems only count after the next PlayerStatsManager.SyncWithUI, which the inventory
    /// already calls when it closes.
    /// </summary>
    public static bool TryMerge(DraggableGem keep, DraggableGem consume)
    {
        if (!CanMerge(keep, consume)) return false;

        keep.Tier++;
        Object.Destroy(consume.gameObject);
        return true;
    }
}

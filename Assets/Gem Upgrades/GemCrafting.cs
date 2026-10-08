using UnityEngine;

/// <summary>
/// Gem tier merging, ready for a crafting UI to call: two gems of the same kind and the same
/// tier become one gem a tier higher (Tier 1 + Tier 1 = Tier 2, Tier 2 + Tier 2 = Tier 3, ...).
/// </summary>
public static class GemCrafting
{
    public static bool CanMerge(DraggableGem a, DraggableGem b)
    {
        return a != null && b != null && a != b
            && a.LinkedGemData != null && a.LinkedGemData == b.LinkedGemData
            && a.Tier == b.Tier
            && a.Tier < DraggableGem.MaxTier;
    }

    /// <summary>
    /// The drag-and-drop merge: <paramref name="dropped"/> was released on <paramref name="target"/>. The target
    /// is the one that levels up, except on the reward board, where the reward is always the one consumed so the
    /// gem the player already owns is the one that grows. Returns false (and does nothing) if they can't merge.
    /// </summary>
    public static bool TryMergeDropped(DraggableGem target, DraggableGem dropped)
    {
        if (!CanMerge(target, dropped)) return false;

        RewardMenuManager reward = RewardMenuManager.Instance;
        bool rewardMode = reward != null && reward.IsRewardModeActive();
        if (rewardMode && !reward.CanDragGem(dropped)) return false;

        DraggableGem keep = target, consume = dropped;
        if (rewardMode && reward.IsActiveRewardGem(target) && !reward.IsActiveRewardGem(dropped))
        {
            keep = dropped;
            consume = target;
        }

        keep.Tier++;

        // Taking the reward by merging counts as choosing it: the other rewards lock, like a normal pick.
        if (rewardMode)
        {
            reward.OnGemMergedFromReward(consume, keep);
            reward.OnGemConsumedByMerge(consume, keep); // the pick must survive its gem being merged away
        }

        // Detach first so the stat sync below can't still count the consumed gem while it waits to be destroyed.
        consume.transform.SetParent(null, false);
        consume.gameObject.SetActive(false);
        Object.Destroy(consume.gameObject);

        keep.PlayMergeFeedback();

        PlayerStatsManager stats = Object.FindFirstObjectByType<PlayerStatsManager>();
        if (stats != null && InventoryManager.Instance != null)
        {
            stats.SyncWithUI(InventoryManager.Instance.primaryGauntlet, InventoryManager.Instance.secondaryGauntlet);
        }

        return true;
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

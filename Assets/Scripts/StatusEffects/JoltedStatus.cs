using UnityEngine;

public class JoltedStatus : BaseStatusEffect
{
    public JoltedStatus(float duration)
    {
        this.StatusID = "Jolted";
        Initialize(duration);
    }

    public override void OnApply(BaseEnemy target)
    {
        target.FreezeForHitstop();
        Debug.Log($"{target.gameObject.name} is stunned!");
    }

    public override void OnTick(BaseEnemy target) { }

    public override void OnRemove(BaseEnemy target)
    {
        target.UnfreezeFromHitstop();
        Debug.Log($"<color=yellow>[STATUS]</color> {target.gameObject.name} snapped out of the shock.");
    }
}

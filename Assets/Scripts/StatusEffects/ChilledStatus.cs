using UnityEngine;

public class ChilledStatus : BaseStatusEffect
{
    private float _slowAmount;

    public ChilledStatus(float slowAmount, float duration)
    {
        this.StatusID = "Slow";
        this._slowAmount = slowAmount;
        Initialize(duration);
    }

    public override void OnApply(BaseEnemy target)
    {
        target.SetSpeedMultiplier(_slowAmount);
        Debug.Log($"{target.gameObject.name} is slowed!");
    }

    public override void OnTick(BaseEnemy target)
    {
        // nothing needs to happen but here just in case
    }

    public override void OnRemove(BaseEnemy target)
    {
        target.ResetSpeedMultiplier();
        Debug.Log($"{target.gameObject.name} is back to normal speed.");
    }
}

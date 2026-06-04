using UnityEngine;

public abstract class BaseStatusEffect
{
    public string StatusID;
    public float Duration;
    protected float timeRemaining;

    public void Initialize(float duration)
    {
        this.Duration = duration;
        this.timeRemaining = duration;
    }

    public abstract void OnApply(BaseEnemy target);

    public abstract void OnTick(BaseEnemy target);

    public abstract void OnRemove(BaseEnemy target);

    public bool UpdateTimer(float deltaTime)
    {
        timeRemaining -= deltaTime;
        return timeRemaining <= 0;
    }
}

using UnityEngine;

public class BurnStatus : BaseStatusEffect
{
    private float _dps;
    private float _tickTimer = 0f;
    
    //BurnStatus Constructor
    public BurnStatus(float damagePerSecond, float duration)
    {
        this.StatusID = "Burn";
        this._dps = damagePerSecond;
        Initialize(duration);
    }

    public override void OnApply(BaseEnemy target)
    {
        // spawn fire particles around enemy
        Debug.Log($"{target.name} caught on fire!");
    }

    public override void OnTick(BaseEnemy target)
    {
        // Deal damage once every second
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= 1f)
        {
            target.TakeDamage(_dps);
            _tickTimer = 0f;
        }
    }

    public override void OnRemove(BaseEnemy target)
    {
        // turn fire particles off
        Debug.Log($"{target.name} stopped burning.");
    }
}

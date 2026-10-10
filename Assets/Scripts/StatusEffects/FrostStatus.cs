using UnityEngine;

/// <summary>
/// Ice gauntlet debuff. Hits add stacks (each slows a little); ten stacks freeze the enemy solid, then it
/// is immune to freezing for a few seconds so it can't be chain-frozen. Stacks fall off if no ice lands for a while.
/// </summary>
public class FrostStatus : BaseStatusEffect
{
    public const int StacksToFreeze = 10;
    private const float StackLifetime = 5f;      // seconds with no ice hit before the stacks drop off
    private const float SlowPerStack = 0.03f;
    private const float FreezeSeconds = 2.5f;
    private const float BossFreezeScale = 0.4f;  // bosses stay frozen for a fraction of the time
    private const float ImmuneSeconds = 3f;

    private enum Phase { Stacking, Frozen, Immune }

    private Phase _phase = Phase.Stacking;
    private int _stacks;
    private float _phaseTimer;
    private readonly float _strength;

    /// <param name="strength">Gem tier multiplier; lengthens the freeze.</param>
    public FrostStatus(float strength)
    {
        StatusID = "Frost";
        _strength = strength;
        Initialize(StackLifetime);
    }

    public int Stacks => _stacks;
    public bool IsFrozen => _phase == Phase.Frozen;

    public void AddStacks(BaseEnemy target, int amount)
    {
        if (_phase != Phase.Stacking) return; // frozen already, or still immune

        _stacks += amount;
        timeRemaining = StackLifetime;

        if (_stacks >= StacksToFreeze) Freeze(target);
        else target.SetSpeedMultiplier(1f - SlowPerStack * _stacks);
    }

    private void Freeze(BaseEnemy target)
    {
        _phase = Phase.Frozen;
        _stacks = 0;

        float seconds = FreezeSeconds * _strength;
        if (target is DemonBoss || target is HellLord) seconds *= BossFreezeScale;
        _phaseTimer = seconds;
        timeRemaining = float.MaxValue; // the phase timer ends this status now, not the stack lifetime

        target.ResetSpeedMultiplier();
        target.FreezeForHitstop();
        HitFlash.For(target).SetFrozen(true);
    }

    private void Thaw(BaseEnemy target)
    {
        target.UnfreezeFromHitstop();
        HitFlash.For(target).SetFrozen(false);
    }

    public override void OnApply(BaseEnemy target) { }

    public override void OnTick(BaseEnemy target)
    {
        if (target.IsDead())
        {
            if (_phase == Phase.Frozen) { Thaw(target); _phase = Phase.Immune; }
            timeRemaining = 0f;
            return;
        }

        if (_phase == Phase.Frozen)
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f)
            {
                Thaw(target);
                _phase = Phase.Immune;
                _phaseTimer = ImmuneSeconds;
            }
        }
        else if (_phase == Phase.Immune)
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f) timeRemaining = 0f;
        }
    }

    public override void OnRemove(BaseEnemy target)
    {
        if (_phase == Phase.Frozen) Thaw(target);
        if (!target.IsDead()) target.ResetSpeedMultiplier();
    }
}

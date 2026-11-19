using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public enum ElementReaction { None, Shatter, Overload, Firestorm, Superconduct, Blizzard, Tempest, Crystallise }

/// <summary>
/// Two gauntlets of different elements react. A punch from an elemental gauntlet marks the enemy with its element
/// for a moment; a punch from the OTHER gauntlet, of another element, inside that moment sets off a reaction and
/// clears the mark. Combos alternate fists, so a pair of elements reacts naturally in a string of hits; a cooldown
/// per enemy keeps it to about one reaction per combo.
///
/// Works from the gauntlets' elements alone; Physical (the plain gauntlet) never marks. The Attunement gem (the
/// passive skill gem) is what makes reactions strong: alone they are half strength, and each gauntlet of the pair with
/// Attunement adds half to the full strength again, more at higher gem tiers (see <see cref="Power"/>).
///
///   Fire + Ice        Shatter       a big burst on the target
///   Fire + Lightning  Overload      an explosion that hits everything around it
///   Fire + Wind       Firestorm     sets everything around it burning
///   Ice + Lightning   Superconduct  locks the target up
///   Ice + Wind        Blizzard      frost on everything around it
///   Lightning + Wind  Tempest       lightning leaps to five enemies
///   Earth + any       Crystallise   a burst, and a crystal shield that takes the player's next hit
/// </summary>
public static class ElementReactions
{
    public const float MarkSeconds = 3f;
    public const float CooldownSeconds = 2.5f;

    // Strength: half with no Attunement; each attuned gauntlet adds 0.5 x its gem tier's multiplier (1 / 1.5 / 2).
    public const float BasePower = 0.5f;
    public const float PowerPerAttunement = 0.5f;
    private const float MinTimeScale = 0.5f, MaxTimeScale = 1.5f; // lock-up and shield last this much less / more at most

    // Sizes, as multiples of the punch that set the reaction off.
    private const float ShatterDamage = 3f;
    private const float OverloadDamage = 1.5f, OverloadRadius = 4f;
    private const float FirestormBurnPerSecond = 0.4f, FirestormSeconds = 4f, FirestormRadius = 5f;
    private const float SuperconductDamage = 1f, SuperconductSeconds = 1.5f, SuperconductBossSeconds = 0.6f;
    private const float BlizzardDamage = 0.5f, BlizzardRadius = 5f;
    private const int BlizzardStacks = 3;
    private const float TempestDamage = 1f, TempestRadius = 8f;
    private const int TempestJumps = 5;
    private const float CrystalliseDamage = 1f, CrystalShieldSeconds = 6f;

    public static readonly Color FireColour = new Color(1f, 0.45f, 0.12f);
    public static readonly Color IceColour = new Color(0.45f, 0.8f, 1f);
    public static readonly Color LightningColour = new Color(1f, 0.92f, 0.3f);
    public static readonly Color WindColour = new Color(0.6f, 1f, 0.85f);
    public static readonly Color EarthColour = new Color(0.75f, 0.55f, 0.3f);

    /// <summary>What these two elements make together (order doesn't matter). None for the same element or Physical.</summary>
    public static ElementReaction Between(ElementType a, ElementType b)
    {
        if (a == b || a == ElementType.Physical || b == ElementType.Physical) return ElementReaction.None;
        if (a == ElementType.Earth || b == ElementType.Earth) return ElementReaction.Crystallise;

        bool Pair(ElementType x, ElementType y) => (a == x && b == y) || (a == y && b == x);
        if (Pair(ElementType.Fire, ElementType.Ice)) return ElementReaction.Shatter;
        if (Pair(ElementType.Fire, ElementType.Lightning)) return ElementReaction.Overload;
        if (Pair(ElementType.Fire, ElementType.Wind)) return ElementReaction.Firestorm;
        if (Pair(ElementType.Ice, ElementType.Lightning)) return ElementReaction.Superconduct;
        if (Pair(ElementType.Ice, ElementType.Wind)) return ElementReaction.Blizzard;
        if (Pair(ElementType.Lightning, ElementType.Wind)) return ElementReaction.Tempest;
        return ElementReaction.None;
    }

    /// <summary>
    /// How strong a reaction is, from the Attunement on each of the two gauntlets: 0.5 with none, +0.5 / 0.75 / 1 for
    /// each attuned gauntlet by its gem tier (I / II / III). One attuned tier I gauntlet = 1 (full strength).
    /// </summary>
    public static float Power(AttackPassive first, AttackPassive second)
    {
        float power = BasePower;
        if (first.Active) power += PowerPerAttunement * first.Strength;
        if (second.Active) power += PowerPerAttunement * second.Strength;
        return power;
    }

    /// <summary>A punch landed: marks the enemy, or sets off a reaction with the other fist's mark.</summary>
    /// <param name="attunement">The punching gauntlet's Attunement (its passive skill gem), if any.</param>
    public static void OnPunch(HitboxController fist, ElementType element, AttackPassive attunement, BaseEnemy enemy, int damage)
    {
        if (element == ElementType.Physical || enemy == null) return;

        ElementMark mark = ElementMark.For(enemy);
        bool reacts = mark.Fist != null && mark.Fist != fist && mark.Element != element
                      && Time.time - mark.Since <= MarkSeconds && Time.time >= mark.CooldownUntil;
        if (reacts)
        {
            ElementReaction reaction = Between(mark.Element, element);
            float power = Power(mark.Attunement, attunement);
            mark.Clear();
            mark.CooldownUntil = Time.time + CooldownSeconds;
            Trigger(reaction, enemy, Mathf.Max(5, damage), power, fist, mark.Element == ElementType.Earth ? element : mark.Element, element);
            return;
        }

        if (!enemy.IsDead()) mark.Set(fist, element, attunement);
    }

    private static void Trigger(ElementReaction reaction, BaseEnemy target, float punch, float power, MonoBehaviour runner, ElementType first, ElementType second)
    {
        Vector3 at = target.transform.position + Vector3.up;
        Color colour = ColourOf(reaction, first, second);
        // Bigger word for a stronger reaction: half strength is small, fully attuned at tier III is large.
        ReactionPopup.Show(at + Vector3.up * 1.4f, Name(reaction), colour, Mathf.Lerp(0.75f, 1.4f, Mathf.InverseLerp(BasePower, 2.5f, power)));

        float hit = punch * power; // every damage and burn below scales with the strength
        float timeScale = Mathf.Clamp(power, MinTimeScale, MaxTimeScale);
        ReactionVfx fx = ReactionVfx.Instance;
        Vector3 feet = target.transform.position;
        float lockSeconds = (target is DemonBoss || target is HellLord || target is InkShadow ? SuperconductBossSeconds : SuperconductSeconds) * timeScale;

        switch (reaction)
        {
            case ElementReaction.Shatter:
                Hurt(target, hit * ShatterDamage);
                EnemyFx.Impact(feet, 2.4f, colour);
                if (fx != null)
                {
                    ReactionVfx.Play(fx.shatterBurst, at, 2.2f);
                    ReactionVfx.Play(fx.shatterCross, at, 1.6f);
                }
                Sound("event:/Environment/Glass_Shatter", at);
                Kick(0.12f);
                break;

            case ElementReaction.Overload:
                foreach (BaseEnemy enemy in Around(feet, OverloadRadius)) Hurt(enemy, hit * OverloadDamage);
                EnemyFx.Impact(feet, OverloadRadius, colour);
                if (fx != null)
                {
                    ReactionVfx.Play(fx.overloadExplosion, at, 2.6f);
                    ReactionVfx.Play(fx.electricHit, at, 2f);
                }
                Sound("event:/Enemies/Fireball_Explode", at);
                Kick(0.18f);
                break;

            case ElementReaction.Firestorm:
                foreach (BaseEnemy enemy in Around(feet, FirestormRadius))
                {
                    StatusManager.For(enemy).ApplyStatus(new BurnStatus(Mathf.Max(1f, hit * FirestormBurnPerSecond), FirestormSeconds));
                    if (fx != null) ReactionVfx.Play(fx.fireHit, enemy.transform.position + Vector3.up, 1.3f);
                }
                EnemyFx.Impact(feet, FirestormRadius, colour);
                if (fx != null) ReactionVfx.Play(fx.fireSpiral, feet + Vector3.up * 0.3f, FirestormRadius * 0.42f);
                Sound("event:/Skills/Skill_Fire", at);
                break;

            case ElementReaction.Superconduct:
                Hurt(target, hit * SuperconductDamage);
                if (!target.IsDead())
                {
                    runner.StartCoroutine(Paralyse(target, lockSeconds));
                    if (fx != null) ReactionVfx.Play(fx.electrified, at, 1.6f, target.transform, lockSeconds);
                }
                EnemyFx.Strike(at, 1.6f, colour);
                if (fx != null) ReactionVfx.Play(fx.iceHit, at, 1.8f);
                Sound("event:/Enemies/Eye_Zap", at);
                Kick(0.08f);
                break;

            case ElementReaction.Blizzard:
                foreach (BaseEnemy enemy in Around(feet, BlizzardRadius))
                {
                    Hurt(enemy, hit * BlizzardDamage);
                    if (fx != null) ReactionVfx.Play(fx.iceHit, enemy.transform.position + Vector3.up, 1.3f);
                    if (enemy.IsDead()) continue;
                    StatusManager status = StatusManager.For(enemy);
                    FrostStatus frost = status.Get<FrostStatus>();
                    if (frost == null) { frost = new FrostStatus(1f); status.ApplyStatus(frost); }
                    frost.AddStacks(enemy, Mathf.Max(1, Mathf.RoundToInt(BlizzardStacks * power)));
                }
                EnemyFx.Impact(feet, BlizzardRadius, colour);
                if (fx != null) ReactionVfx.Play(fx.iceSpiral, feet + Vector3.up * 0.3f, BlizzardRadius * 0.42f);
                Sound("event:/Skills/Skill_Ice", at);
                break;

            case ElementReaction.Tempest:
                Hurt(target, hit * TempestDamage);
                AttackPassiveEffects.Chain(runner, target, hit * TempestDamage, TempestJumps, TempestRadius,
                    jumped => { if (fx != null) ReactionVfx.Play(fx.electricHit, jumped.transform.position + Vector3.up, 1.4f); });
                EnemyFx.Strike(at, 1.8f, colour);
                if (fx != null)
                {
                    ReactionVfx.Play(fx.electricHit, at, 1.8f);
                    ReactionVfx.Play(fx.windTrails, feet + Vector3.up * 0.5f, 1.5f, null, 1.2f);
                }
                Sound("event:/Enemies/Eye_Zap", at);
                break;

            case ElementReaction.Crystallise:
                Hurt(target, hit * CrystalliseDamage);
                EnemyFx.GroundCrack(feet, 2f, colour);
                if (fx != null) ReactionVfx.Play(fx.groundHit, feet, 1.8f);
                Sound("event:/Environment/Glass_Shatter", at);
                Kick(0.08f);
                PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
                if (player != null) player.GrantCrystalShield(CrystalShieldSeconds * timeScale, colour);
                break;
        }
    }

    private static void Kick(float amount)
    {
        if (PlayerCamera.Instance != null) PlayerCamera.Instance.Kick(Vector3.up, amount);
    }

    // Reaction damage is quiet: the punch already played the hit feedback.
    private static void Hurt(BaseEnemy enemy, float damage)
    {
        if (enemy == null || enemy.IsDead()) return;
        HitFeel.InMelee = true;
        try { enemy.TakeDamage(Mathf.Max(1f, damage)); }
        finally { HitFeel.InMelee = false; }
        if (!enemy.IsDead()) HitFlash.For(enemy).Flash(0.07f);
    }

    // Locked up: the same freeze the Frost status uses (it stacks safely with hit-stops and other freezes).
    private static System.Collections.IEnumerator Paralyse(BaseEnemy target, float seconds)
    {
        target.FreezeForHitstop();
        yield return new WaitForSeconds(seconds);
        if (target != null) target.UnfreezeFromHitstop();
    }

    private static List<BaseEnemy> Around(Vector3 centre, float radius)
    {
        var found = new List<BaseEnemy>();
        foreach (Collider col in Physics.OverlapSphere(centre, radius))
        {
            BaseEnemy enemy = col.GetComponentInParent<BaseEnemy>();
            if (enemy != null && !enemy.IsDead() && !found.Contains(enemy)) found.Add(enemy);
        }
        return found;
    }

    private static void Sound(string path, Vector3 at) => FMODUnity.RuntimeManager.PlayOneShot(path, at);

    public static string Name(ElementReaction reaction) =>
        new LocalizedString("UIStrings", "React_" + reaction).GetLocalizedString();

    public static Color ColourOf(ElementReaction reaction, ElementType first, ElementType second) => reaction switch
    {
        ElementReaction.Shatter => IceColour,
        ElementReaction.Overload => Color.Lerp(FireColour, LightningColour, 0.5f),
        ElementReaction.Firestorm => FireColour,
        ElementReaction.Superconduct => Color.Lerp(IceColour, LightningColour, 0.4f),
        ElementReaction.Blizzard => Color.Lerp(IceColour, WindColour, 0.4f),
        ElementReaction.Tempest => LightningColour,
        ElementReaction.Crystallise => EarthColour,
        _ => Color.white,
    };
}

/// <summary>The element a punch left on an enemy, waiting for the other fist (see <see cref="ElementReactions"/>).</summary>
public class ElementMark : MonoBehaviour
{
    public HitboxController Fist { get; private set; }
    public ElementType Element { get; private set; }
    public float Since { get; private set; }
    public AttackPassive Attunement { get; private set; }
    public float CooldownUntil;

    public static ElementMark For(BaseEnemy enemy)
    {
        ElementMark mark = enemy.GetComponent<ElementMark>();
        return mark != null ? mark : enemy.gameObject.AddComponent<ElementMark>();
    }

    public void Set(HitboxController fist, ElementType element, AttackPassive attunement)
    {
        Fist = fist;
        Element = element;
        Attunement = attunement;
        Since = Time.time;
    }

    public void Clear() => Fist = null;
}

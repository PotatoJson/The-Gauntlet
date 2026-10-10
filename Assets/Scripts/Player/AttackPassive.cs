using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the gauntlet that threw a punch adds to it: the passive skill gem socketed in that gauntlet,
/// resolved against the gauntlet's element. Default (Active = false) is a plain punch.
/// </summary>
public struct AttackPassive
{
    public bool Active;
    public ElementType Element;
    public int Tier;

    /// <summary>Higher tiers push the elemental effects harder (same curve as skill damage).</summary>
    public float Strength => GemTierInfo.SkillDamageMultiplier(Tier);
}

/// <summary>On-hit effects of the passive skill gem, by element. Earth and Wind change how the swing itself
/// behaves and live in PlayerCombat / PlayerHealth instead.</summary>
public static class AttackPassiveEffects
{
    // Fire: basic attacks set the enemy alight.
    private const float BurnDamageFraction = 0.3f; // of the hit, every second
    private const float BurnSeconds = 4f;

    // Lightning: the hit arcs on to the nearest enemies.
    private const int LightJumps = 2;
    private const float LightRadius = 6f;
    private const float LightDamageFraction = 0.4f;
    private const int HeavyJumps = 3;
    private const float HeavyRadius = 9f;
    private const float HeavyDamageFraction = 0.6f;
    private const float JumpDelay = 0.08f;

    // Ice: stacks of frost.
    private const int LightStacks = 1;
    private const int HeavyStacks = 2;

    private static Material _zapMaterial;

    public static void OnHit(AttackPassive passive, BaseEnemy enemy, int damage, CombatInput attack, MonoBehaviour runner)
    {
        if (!passive.Active || enemy == null) return;

        switch (passive.Element)
        {
            case ElementType.Fire:
                if (attack == CombatInput.Light && !enemy.IsDead())
                {
                    float dps = Mathf.Max(1f, damage * BurnDamageFraction * passive.Strength);
                    StatusManager.For(enemy).ApplyStatus(new BurnStatus(dps, BurnSeconds));
                }
                break;

            case ElementType.Ice:
                if (!enemy.IsDead())
                {
                    StatusManager status = StatusManager.For(enemy);
                    FrostStatus frost = status.Get<FrostStatus>();
                    if (frost == null)
                    {
                        frost = new FrostStatus(passive.Strength);
                        status.ApplyStatus(frost);
                    }
                    frost.AddStacks(enemy, attack == CombatInput.Heavy ? HeavyStacks : LightStacks);
                }
                break;

            case ElementType.Lightning:
                bool heavy = attack == CombatInput.Heavy;
                runner.StartCoroutine(ChainRoutine(enemy,
                    damage * (heavy ? HeavyDamageFraction : LightDamageFraction) * passive.Strength,
                    heavy ? HeavyJumps : LightJumps,
                    heavy ? HeavyRadius : LightRadius));
                break;
        }
    }

    private static IEnumerator ChainRoutine(BaseEnemy first, float damage, int jumps, float radius)
    {
        var alreadyHit = new HashSet<BaseEnemy> { first };
        BaseEnemy from = first;

        for (int i = 0; i < jumps; i++)
        {
            yield return new WaitForSeconds(JumpDelay);
            if (from == null) yield break;

            BaseEnemy next = FindNearest(from.transform.position, radius, alreadyHit);
            if (next == null) yield break;

            alreadyHit.Add(next);
            Zap(from.transform.position + Vector3.up, next.transform.position + Vector3.up);

            // Quiet damage: the punch itself already played the hit feedback.
            HitFeel.InMelee = true;
            try { next.TakeDamage(Mathf.Max(1f, damage)); }
            finally { HitFeel.InMelee = false; }

            if (!next.IsDead()) HitFlash.For(next).Flash(0.07f);

            from = next;
        }
    }

    private static BaseEnemy FindNearest(Vector3 position, float radius, HashSet<BaseEnemy> exclude)
    {
        BaseEnemy best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider col in Physics.OverlapSphere(position, radius))
        {
            BaseEnemy candidate = col.GetComponentInParent<BaseEnemy>();
            if (candidate == null || candidate.IsDead() || exclude.Contains(candidate)) continue;

            float distance = (candidate.transform.position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
        return best;
    }

    // A short, jagged yellow line between two enemies.
    private static void Zap(Vector3 from, Vector3 to)
    {
        if (_zapMaterial == null)
        {
            Material flat = Resources.Load<Material>("HitFlashWhite");
            if (flat == null) return;

            _zapMaterial = new Material(flat);
            Color yellow = new Color(1f, 0.93f, 0.35f, 1f);
            _zapMaterial.SetColor("_BaseColor", yellow);
            _zapMaterial.SetColor("_Color", yellow);
        }

        var go = new GameObject("ChainZap");
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = _zapMaterial;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.widthMultiplier = 0.1f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        const int points = 7;
        line.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            Vector3 p = Vector3.Lerp(from, to, t);
            if (i > 0 && i < points - 1) p += Random.insideUnitSphere * 0.25f; // jag the inside points only
            line.SetPosition(i, p);
        }

        Object.Destroy(go, 0.12f);
    }
}

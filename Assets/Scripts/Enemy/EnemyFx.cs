using UnityEngine;

/// <summary>
/// One-line access to the attack effects the later enemies and the bosses share. The prefabs live in
/// Resources/EnemyFx, so an enemy script needs no references wired up in the inspector.
///
///   Ring    a glowing circle that follows an enemy (a boss's aura, the Beyblade's spin ring)
///   Impact  a flash, shockwave and embers where a heavy hit lands
///   Slash   a crescent swept in front of an enemy for a sword swing
///   Bite    snapping jaws for the Biter
/// Every call quietly does nothing when its prefab is missing, so a combat script never breaks over a visual.
/// </summary>
public static class EnemyFx
{
    public static readonly Color Fire = new Color(1f, 0.45f, 0.12f, 1f);
    public static readonly Color Rage = new Color(1f, 0.2f, 0.1f, 1f);
    public static readonly Color Hell = new Color(0.95f, 0.15f, 0.45f, 1f);

    private static GameObject Load(string name) => Resources.Load<GameObject>("EnemyFx/" + name);

    /// <summary>A circle that follows <paramref name="follow"/> until the enemy dies or <see cref="GroundFx.End"/> is called.</summary>
    public static GroundFx Ring(Transform follow, float radius, Color tint, float lift = 0f, float spinDegrees = 25f)
    {
        GameObject prefab = Load("EnemyGroundFx");
        if (prefab == null || follow == null) return null;

        GroundFx fx = Object.Instantiate(prefab, follow.position, Quaternion.identity).GetComponent<GroundFx>();
        fx.SetSpin(spinDegrees);
        fx.Begin(radius, follow, tint, lift);
        return fx;
    }

    /// <summary>A burst on the floor at (or under) this point.</summary>
    public static void Impact(Vector3 position, float radius, Color tint)
    {
        GameObject prefab = Load("EnemyImpactFx");
        if (prefab == null) return;

        ImpactFx fx = Object.Instantiate(prefab, FloorUnder(position), Quaternion.identity).GetComponent<ImpactFx>();
        fx.Play(radius, tint);
    }

    /// <summary>A crescent swung across the front of <paramref name="owner"/>, sweeping through this many degrees.</summary>
    public static void Slash(Transform owner, float radius, float height, Color tint, float sweepDegrees = 150f, float seconds = 0.28f)
    {
        GameObject prefab = Load("SlashFx");
        if (prefab == null || owner == null) return;

        Vector3 position = owner.position + Vector3.up * height;
        Quaternion facing = Quaternion.Euler(0f, owner.eulerAngles.y, 0f);
        Object.Instantiate(prefab, position, facing).GetComponent<SlashFx>().Play(radius, tint, sweepDegrees, seconds);
    }

    /// <summary>A pair of jaws snapping shut at this point, turned to face the camera.</summary>
    public static void Bite(Vector3 position)
    {
        GameObject prefab = Load("BiteFx");
        if (prefab == null) return;

        Object.Instantiate(prefab, position, Quaternion.identity);
    }

    // The floor below a point; characters standing there don't count as floor.
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private static Vector3 FloorUnder(Vector3 point)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 1.5f, Vector3.down, Hits, 8f, ~0, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        Vector3 floor = point;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            if (hit.collider.GetComponentInParent<BaseEnemy>() != null) continue;

            if (hit.distance < best)
            {
                best = hit.distance;
                floor = hit.point;
            }
        }

        return floor + Vector3.up * 0.05f;
    }
}

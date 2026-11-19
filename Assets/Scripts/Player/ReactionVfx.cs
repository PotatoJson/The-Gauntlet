using UnityEngine;

/// <summary>
/// The particle effects each element reaction plays (Cartoon FX Remaster prefabs). One asset, at
/// Resources/Fx/ReactionVfx, read by <see cref="ElementReactions"/>; being in Resources keeps the prefabs in builds.
/// </summary>
[CreateAssetMenu(menuName = "Gauntlet/Reaction VFX")]
public class ReactionVfx : ScriptableObject
{
    [Header("Shatter (Fire + Ice)")]
    public GameObject shatterBurst;
    public GameObject shatterCross;

    [Header("Overload (Fire + Lightning)")]
    public GameObject overloadExplosion;
    public GameObject electricHit;

    [Header("Firestorm (Fire + Wind)")]
    public GameObject fireSpiral;
    public GameObject fireHit;

    [Header("Superconduct (Ice + Lightning)")]
    [Tooltip("Loops: stays on the enemy while it's locked up.")]
    public GameObject electrified;
    public GameObject iceHit;

    [Header("Blizzard (Ice + Wind)")]
    public GameObject iceSpiral;

    [Header("Tempest (Lightning + Wind)")]
    [Tooltip("Loops: a gust round the target for a moment.")]
    public GameObject windTrails;

    [Header("Crystallise (Earth + any)")]
    public GameObject groundHit;
    [Tooltip("Loops: round the player while the crystal shield is up.")]
    public GameObject shieldAura;
    public GameObject shieldBreak;

    private static ReactionVfx _instance;
    private static bool _looked;

    public static ReactionVfx Instance
    {
        get
        {
            if (!_looked) { _instance = Resources.Load<ReactionVfx>("Fx/ReactionVfx"); _looked = true; }
            return _instance;
        }
    }

    /// <summary>
    /// Plays a prefab at a point, this much bigger, optionally riding on something. Looping effects are removed after
    /// <paramref name="seconds"/>; one-shots clear themselves (their CFXR_Effect destroys them when done).
    /// </summary>
    public static GameObject Play(GameObject prefab, Vector3 at, float scale = 1f, Transform follow = null, float seconds = 0f)
    {
        if (prefab == null) return null;
        GameObject fx = Object.Instantiate(prefab, at, Quaternion.identity, follow);
        fx.transform.localScale *= scale;
        if (seconds > 0f) Object.Destroy(fx, seconds);
        return fx;
    }
}

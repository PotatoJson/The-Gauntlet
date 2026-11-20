using UnityEngine;

/// <summary>
/// The Spirit Bow's side-on stance. While the bow is raised, the bow layer plays the arms (Bow_Draw / Bow_Loose, baked
/// on the knight by BowPoseBaker) but leaves the spine to the walk; this turns the chest after the animation so the
/// shoulders line up with the shot, the same turn the clips' arms were posed on, and tilts it to the shot's height.
///
/// Lives on the Animator's object; runs after the Animator (LateUpdate) and before <see cref="SpiritBowVisual"/> puts
/// the bow on the hands. <see cref="PlayerBow"/> sets the fields each frame.
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Animator))]
public class BowStance : MonoBehaviour
{
    /// <summary>Degrees the shoulder line turns from square to the shot. The bow clips were baked on this: re-bake them
    /// (Tools > Bow > Bake Draw Clips) after changing it.</summary>
    public const float Stance = 85f;
    private const float MaxPitch = 45f;
    private const float MaxTurn = 50f;      // most the shot may be off the way his body faces

    /// <summary>0 = bow carried (nothing done here), 1 = raised to aim.</summary>
    [System.NonSerialized] public float Raise;
    /// <summary>The way the shot goes.</summary>
    [System.NonSerialized] public Vector3 Aim = Vector3.forward;

    private Animator _animator;

    private void Awake() => _animator = GetComponent<Animator>();

    private void LateUpdate()
    {
        if (Raise <= 0.001f) return;
        float w = Raise * Raise * (3f - 2f * Raise);
        Vector3 aim = Aim.sqrMagnitude > 0.001f ? Aim.normalized : transform.forward;
        Vector3 body = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up);
        flat = flat.sqrMagnitude > 0.001f ? flat.normalized : body;
        // Never more than MaxTurn off the way he faces (he turns to the camera himself): whatever the aim does, the
        // chest can't wind round.
        float off = Mathf.Clamp(Vector3.SignedAngle(body, flat, Vector3.up), -MaxTurn, MaxTurn);
        flat = Quaternion.AngleAxis(off, Vector3.up) * body;

        TurnSideOn(_animator, flat, w);

        // Up or down the shot: the chest tilts about the line across the shot, carrying the bow arm and the draw with it.
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg, -MaxPitch, MaxPitch) * w;
        Vector3 across = Vector3.Cross(Vector3.up, flat);
        Transform[] spine = Spine(_animator);
        foreach (Transform bone in spine) bone.rotation = Quaternion.AngleAxis(-pitch / spine.Length, across) * bone.rotation;
    }

    /// <summary>Turns the chest (spread down the spine) until the shoulder line sits <see cref="Stance"/> degrees round
    /// from square to <paramref name="shot"/> (a level direction), by <paramref name="weight"/> of the way. Measured from
    /// wherever the animation left it, so the walk underneath doesn't matter.</summary>
    public static void TurnSideOn(Animator animator, Vector3 shot, float weight)
    {
        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (left == null || right == null) return;
        Vector3 shoulders = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up);
        Vector3 wanted = Quaternion.AngleAxis(Stance, Vector3.up) * Vector3.Cross(Vector3.up, shot);
        float yaw = Vector3.SignedAngle(shoulders, wanted, Vector3.up) * weight;
        Transform[] spine = Spine(animator);
        foreach (Transform bone in spine) bone.rotation = Quaternion.AngleAxis(yaw / spine.Length, Vector3.up) * bone.rotation;
    }

    private static Transform[] Spine(Animator animator) => System.Array.FindAll(new[]
    {
        animator.GetBoneTransform(HumanBodyBones.Spine), animator.GetBoneTransform(HumanBodyBones.Chest), animator.GetBoneTransform(HumanBodyBones.UpperChest),
    }, b => b != null);
}

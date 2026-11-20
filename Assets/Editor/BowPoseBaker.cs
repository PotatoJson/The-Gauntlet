using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Bakes the Spirit Bow's arm clips on the knight's own rig (Tools > Bow > Bake Draw Clips), since the library bow clips
/// were made for a slimmer body: on the knight their string hand ends up inside the chest.
///
/// The pose is built side-on: shoulders in line with the shot, head turned to look down it, the arrow running past the
/// chin at <see cref="Side"/> metres out from the neck (clear of the helmet). The bow arm reaches straight along that line;
/// the string hand travels back along it, from just ahead of the chin to the anchor behind it, its elbow swinging from
/// out in front of the chest round to behind the shoulder, so the arm never passes through the body.
///
///  - Bow_Draw (1 s): the draw, 0 = just nocked, 1 = full. Played by draw amount (motion time), not by time.
///  - Bow_Loose (0.4 s): the release: the hand flicks back past the jaw, then comes forward to nock the next arrow.
///
/// Fingers and hand grips come from the library's Bow_Aim_Neutral; everything else is placed here.
///
/// The clips carry the arms, hands and head (each relative to the bone it hangs from). The spine stays the walk's: the
/// bow layer's mask leaves it out, and <see cref="BowStance"/> turns the chest side-on after the animation, exactly as
/// it is turned here before the arms are posed, so they come out the same in game.
/// </summary>
public static class BowPoseBaker
{
    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string Library = "Assets/Models/UAL2.fbx";
    private const string Folder = "Assets/Animation/Bow";
    public const string DrawPath = Folder + "/Bow_Draw.anim", LoosePath = Folder + "/Bow_Loose.anim";

    // The draw, in metres on the knight. (The side-on turn itself is BowStance's, shared with the game.)
    public static float Side = 0.32f;       // the arrow's line, out from the neck towards the chest side (leaves a gap between the string hand and the chest)
    public static float NockedElbowOut = 0.3f;    // string elbow at the start of a draw: out in front of the chest...
    public static float NockedElbowLift = -0.3f;  // ... and this far below the shoulder (higher brings the upper arm in to the helmet)
    public static float Drop = 0.05f;       // ... and this far below the neck joint (under the helmet's chin)
    public static float Ahead = 0.08f;      // the chin, ahead of the neck
    public static float Nocked = 0.14f;     // string hand at the start of a draw, ahead of the chin
    public static float Anchor = -0.05f;    // ... at full draw (behind the chin)
    public static float BowLength = 0.72f;  // chin to bow hand, along the shot
    public static float GripBeside = 0.15f; // the bow wrist sits this far to the left of the arrow's line...
    public static float GripBelow = 0.10f;  // ... and this far under it, so the arrow rests along the fist's right side
                                            // (SpiritBowVisual.ArrowBeside/Above), parallel to the shot
    public static float FistCurl = -0.9f;   // left fingers' "Stretched" muscles round the bow (-1 = curled shut)
    public static float ThumbCurl = -0.9f;  // ... and the thumb wrapped round it too

    [MenuItem("Tools/Bow/Bake Draw Clips")]
    public static void Bake()
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        try
        {
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.identity);
            Animator animator = root.GetComponentInChildren<Animator>();
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);

            var draw = new List<(float time, HumanPose pose)>();
            for (int i = 0; i <= 4; i++)
            {
                float pull = i / 4f;
                Pose(animator, pull, 0f);
                draw.Add((pull, Read(handler)));
            }
            var loose = new List<(float time, HumanPose pose)>();
            Pose(animator, 1f, 0f); loose.Add((0f, Read(handler)));
            Pose(animator, 1f, 1f); loose.Add((0.07f, Read(handler)));
            Pose(animator, 0.5f, 0.3f); loose.Add((0.2f, Read(handler)));
            Pose(animator, 0f, 0f); loose.Add((0.4f, Read(handler)));

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animation", "Bow");
            Write(DrawPath, draw);
            Write(LoosePath, loose);
            handler.Dispose();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BowPoseBaker] baked " + DrawPath + " and " + LoosePath);
    }

    /// <summary>Puts the rig in the bow pose: <paramref name="pull"/> 0 = nocked, 1 = full draw; <paramref name="loose"/>
    /// 1 = the hand flicked back just after a release.</summary>
    public static void Pose(Animator animator, float pull, float loose)
    {
        Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);
        Vector3 up = Vector3.up, forward = animator.transform.forward, side = Vector3.Cross(up, forward);

        // Which way the face points, from a plain idle; then the library's bow pose (for its hands and fingers), on the
        // idle's hips: in game the hips are the walk's, facing forward, and the stance is built relative to them.
        // The spine too: in game it's the walk's (the bow layer leaves it alone) and BowStance turns it side-on after.
        Sample(animator, "Armature|Idle_No_Loop", 0f);
        Transform head = Bone(HumanBodyBones.Head), hips = Bone(HumanBodyBones.Hips);
        Vector3 faceLocal = Quaternion.Inverse(head.rotation) * forward;
        var trunk = new List<Transform>();
        foreach (var b in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head })
            if (Bone(b) != null) trunk.Add(Bone(b));
        var trunkRotations = trunk.ConvertAll(t => t.localRotation);
        Vector3 hipsPosition = hips.localPosition;
        Sample(animator, "Armature|Bow_Aim_Neutral", 0.5f);
        for (int i = 0; i < trunk.Count; i++) trunk[i].localRotation = trunkRotations[i];
        hips.localPosition = hipsPosition;

        // Side-on, exactly as BowStance turns it in game.
        Transform lUpper = Bone(HumanBodyBones.LeftUpperArm), rUpper = Bone(HumanBodyBones.RightUpperArm);
        BowStance.TurnSideOn(animator, forward, 1f);

        // The head looks down the shot.
        Vector3 face = Vector3.ProjectOnPlane(head.rotation * faceLocal, up);
        head.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(face, forward, up), up) * head.rotation;

        // The arrow's line: past the chin, out on the chest side, under the helmet.
        Vector3 chin = Bone(HumanBodyBones.Neck).position + forward * Ahead + side * Side - up * Drop;

        // Bow arm: straight out to the bow, the fist just left of and under the arrow's line.
        Transform lLower = Bone(HumanBodyBones.LeftLowerArm), lHand = Bone(HumanBodyBones.LeftHand);
        float reach = Vector3.Distance(lUpper.position, lLower.position) + Vector3.Distance(lLower.position, lHand.position);
        Vector3 toBow = chin + forward * BowLength - side * GripBeside - up * GripBelow - lUpper.position;
        Vector3 bowHand = lUpper.position + toBow.normalized * Mathf.Min(toBow.magnitude, reach * 0.97f);
        Solve(lUpper, lLower, lHand, bowHand, (lUpper.position + bowHand) * 0.5f - up * 0.3f - side * 0.1f);

        // The bow fist stands upright round the grip, as an archer holds it: knuckles towards the target, thumb on top,
        // palm to the right (the fingers are curled shut in Read).
        Transform middle = Bone(HumanBodyBones.LeftMiddleProximal), thumb = Bone(HumanBodyBones.LeftThumbProximal);
        if (middle != null && thumb != null)
        {
            Vector3 fingers = (middle.position - lHand.position).normalized;
            Vector3 thumbSide = Vector3.ProjectOnPlane(thumb.position - lHand.position, fingers);
            Vector3 wantFingers = toBow.normalized;
            Vector3 wantThumb = Vector3.ProjectOnPlane(up, wantFingers);
            lHand.rotation = Quaternion.LookRotation(wantFingers, wantThumb) * Quaternion.Inverse(Quaternion.LookRotation(fingers, thumbSide)) * lHand.rotation;
        }

        // String hand: along the line from just ahead of the chin back to the anchor; elbow from out in front of the
        // chest round to behind the shoulder. Loose: flicked back and out past the jaw.
        Transform rLower = Bone(HumanBodyBones.RightLowerArm), rHand = Bone(HumanBodyBones.RightHand);
        float p = pull * pull * (3f - 2f * pull);
        Vector3 hand = chin + forward * Mathf.Lerp(Nocked, Anchor, p) + (-forward * 0.14f + side * 0.08f + up * 0.02f) * loose;
        Vector3 hintNocked = rUpper.position + side * NockedElbowOut + up * NockedElbowLift + forward * 0.1f;
        Vector3 hintDrawn = rUpper.position - forward * 0.45f + up * 0.05f + side * 0.05f;
        Solve(rUpper, rLower, rHand, hand - forward * 0.06f, Vector3.Lerp(hintNocked, hintDrawn, p));
    }

    private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();

    private static void Sample(Animator animator, string clipName, float time)
    {
        if (!Clips.TryGetValue(clipName, out AnimationClip clip) || clip == null)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Library))
                if (o is AnimationClip c && c.name == clipName) clip = c;
            Clips[clipName] = clip;
        }
        PlayableGraph graph = PlayableGraph.Create();
        var output = AnimationPlayableOutput.Create(graph, "pose", animator);
        var playable = AnimationClipPlayable.Create(graph, clip);
        PlayableOutputExtensions.SetSourcePlayable(output, playable);
        graph.Play();
        PlayableExtensions.SetTime(playable, time);
        graph.Evaluate(0f);
        graph.Destroy();
    }

    // Two-bone IK: the end on the target, the middle joint bent towards the hint. The end keeps its local rotation.
    private static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 hint)
    {
        Vector3 root = upper.position;
        float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, end.position);
        Vector3 to = target - root;
        float d = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
        Vector3 dir = to.normalized;
        Vector3 bend = Vector3.Cross(dir, hint - root);
        if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dir, Vector3.down);
        float angle = Mathf.Acos(Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 elbow = root + Quaternion.AngleAxis(angle, bend.normalized) * dir * a;
        upper.rotation = Quaternion.FromToRotation(lower.position - root, elbow - root) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, root + dir * d - lower.position) * lower.rotation;
    }

    private static HumanPose Read(HumanPoseHandler handler)
    {
        var pose = new HumanPose();
        handler.GetHumanPose(ref pose);
        // The bow fist, closed round the grip.
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            string name = HumanTrait.MuscleName[i];
            if (!name.StartsWith("Left ") || !name.EndsWith("Stretched")) continue;
            if (name.Contains("Index") || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Little")) pose.muscles[i] = FistCurl;
            else if (name.Contains("Thumb")) pose.muscles[i] = ThumbCurl;
        }
        return pose;
    }

    // A muscle's name in a clip. Body muscles are the same as HumanTrait's, but fingers aren't: "Left Index 1 Stretched"
    // is bound as "LeftHand.Index.1 Stretched" (and "Left Index Spread" as "LeftHand.Index.Spread"). Under the
    // HumanTrait name a finger curve binds to nothing and the fingers sit at neutral.
    private static string ClipName(string muscle)
    {
        string[] part = muscle.Split(' ');
        bool finger = part.Length >= 3 && (part[0] == "Left" || part[0] == "Right")
                      && System.Array.IndexOf(new[] { "Thumb", "Index", "Middle", "Ring", "Little" }, part[1]) >= 0;
        if (!finger) return muscle;
        string hand = part[0] + "Hand." + part[1] + ".";
        return part.Length == 4 ? hand + part[2] + " " + part[3] : hand + part[2];
    }

    private static void Write(string path, List<(float time, HumanPose pose)> keys)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();

        void Curve(string property, System.Func<HumanPose, float> value)
        {
            var curve = new AnimationCurve();
            foreach (var (time, pose) in keys) curve.AddKey(time, value(pose));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }

        for (int m = 0; m < HumanTrait.MuscleCount; m++) { int i = m; Curve(ClipName(HumanTrait.MuscleName[i]), p => p.muscles[i]); }
        Curve("RootT.x", p => p.bodyPosition.x); Curve("RootT.y", p => p.bodyPosition.y); Curve("RootT.z", p => p.bodyPosition.z);
        Curve("RootQ.x", p => p.bodyRotation.x); Curve("RootQ.y", p => p.bodyRotation.y);
        Curve("RootQ.z", p => p.bodyRotation.z); Curve("RootQ.w", p => p.bodyRotation.w);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }
}

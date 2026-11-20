using UnityEngine;

/// <summary>
/// The Spirit Bow as you see it: two glowing limbs (top half in one gauntlet's colour, bottom half in the other's when
/// both bows are out) and a string, always in the left hand. Carried, it hangs from the left fist, limbs front and back,
/// string across the forearm; raised, it's held out at the target. The right hand takes the string only while a shot is
/// drawn (it pulls back as the hand does, see the Bow_Draw clip); loosed, the string snaps back straight with a twang.
/// Placed every frame after the animation.
///
/// While a shot is drawn, a spirit arrow forms on the string, in the next arrow's colour: it grows brighter as the
/// string comes back, motes of light gather into its tip, and a ring in front of the bow tightens onto the line of the
/// shot. At full draw the tip flares and keeps pulsing until the arrow is loosed.
/// </summary>
public class SpiritBowVisual : MonoBehaviour
{
    private const int LimbPoints = 10;
    private const float LimbLength = 0.85f;  // hand to tip
    private const float LimbBack = 0.28f;    // how far the tips sweep back towards the archer
    private const float Glow = 1.35f;        // a little over 1 so the bloom picks it up; more washes the colour to white
    private const float Cant = 15f;          // degrees the bow leans, as archers hold it (and so it reads as a bow from behind)
    private const float ArrowReach = 0.6f;   // how far the arrow's tip sits in front of the bow (its glow kept clear of the fist)
    private const float ArrowBeside = 0.11f; // the arrow rests against the right of the bow fist (the gauntlet's fist is ~0.22
    private const float ArrowAbove = 0.04f;  // across; measured on the mesh), near its top, as on an archer's knuckle
    private const float Riser = 0.08f;       // the limbs start this far from the middle of the fist (the handle), so the fist shows
    private const string Folder = "ChargePulse/";

    private Animator _animator;
    private Transform _player;
    private LineRenderer _top, _bottom, _string, _arrow;
    private Light _light;
    private Color _topColour, _bottomColour, _arrowColour = Color.white;
    private float _draw, _shown, _raised, _arrowShown;
    private float _held;                    // how firmly the right hand has the string (0 = not touching it)
    private Vector3 _twang;                 // the string's pull when it was let go
    private float _twangAge = 10f;
    private Vector3 _aim = Vector3.forward;
    private bool _wasFull;
    private float _ringTimer;

    private ParticleSystem _motes, _rings, _tip;

    /// <summary>Where an arrow leaves the bow: the spirit arrow's tip.</summary>
    public Vector3 Nock { get; private set; }

    public void Show(Animator animator, Transform player, Color top, Color bottom)
    {
        _animator = animator;
        _player = player;
        _topColour = top;
        _bottomColour = bottom;
        if (_top == null)
        {
            _top = Line("Upper limb", 0.06f, 0.025f);
            _bottom = Line("Lower limb", 0.06f, 0.025f);
            _string = Line("String", 0.015f, 0.015f);
            _arrow = Line("Spirit arrow", 0.025f, 0.07f);
            _light = new GameObject("Glow").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.type = LightType.Point;
            _light.range = 3.5f;
            _light.shadows = LightShadows.None;
            BuildCharge();
        }
        Paint();
    }

    /// <summary>How far the string is drawn (0-1), how much the bow is shown (0 = put away for a moment), and how far
    /// it's raised to aim (0 = carried at his side, 1 = up in the left hand).</summary>
    public void SetDrawn(float draw, float shown, float raised)
    {
        _draw = draw;
        _shown = shown;
        _raised = raised * raised * (3f - 2f * raised);
        Paint();
    }

    /// <summary>An arrow was loosed: the string springs back straight and quivers. A quick shot (never drawn) twangs
    /// as if pulled a little.</summary>
    public void Loose()
    {
        if (_held <= 0.01f) _twang = -_aim * 0.12f;
        _twangAge = 0f;
        _held = 0f;
    }

    /// <summary>The way the next arrow will fly, and its colour: the spirit arrow is drawn along it.</summary>
    public void SetShot(Vector3 aim, Color colour)
    {
        if (aim.sqrMagnitude > 0.001f) _aim = aim.normalized;
        _arrowColour = colour;
    }

    private LineRenderer Line(string name, float start, float end)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = true;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startWidth = start;
        line.endWidth = end;
        line.numCapVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return line;
    }

    private void Paint()
    {
        if (_top == null) return;
        SetColour(_top, _topColour, 1f);
        SetColour(_bottom, _bottomColour, 1f);
        SetColour(_string, Color.Lerp(_topColour, _bottomColour, 0.5f), 0.8f);
        _light.color = Color.Lerp(Color.Lerp(_topColour, _bottomColour, 0.5f), _arrowColour, _draw);
        _light.intensity = 1.6f * _shown * (1f + 1.2f * _draw);
    }

    private void SetColour(LineRenderer line, Color colour, float alpha)
    {
        Color hdr = colour * Glow;
        hdr.a = alpha * _shown;
        line.material.color = hdr;
    }

    private void LateUpdate()
    {
        if (_animator == null || _player == null) return;
        Transform leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (leftHand == null) return;
        Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform fingers = _animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        if (fingers == null) fingers = rightHand;

        Vector3 forward = _player.forward;
        Vector3 flatAim = Vector3.ProjectOnPlane(_aim, Vector3.up);
        flatAim = flatAim.sqrMagnitude > 0.001f ? flatAim.normalized : forward;

        // Held in the left fist, always: through the hole the curled middle finger makes (its three joints), not the
        // knuckles, so the fingers wrap the handle.
        Transform k1 = _animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        Transform k2 = _animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate);
        Transform k3 = _animator.GetBoneTransform(HumanBodyBones.LeftMiddleDistal);
        Vector3 grip = k1 != null && k2 != null && k3 != null ? (k1.position + k2.position + k3.position) / 3f
                     : k1 != null ? Vector3.Lerp(leftHand.position, k1.position, 0.6f) : leftHand.position;
        // Aimed: upright, square to the shot, leaning its top away to the right, string towards him.
        Vector3 aimUp = Vector3.ProjectOnPlane(Quaternion.AngleAxis(-Cant, flatAim) * Vector3.up, _aim).normalized;
        // Carried: hanging from the fist, limbs front and back curving down below it, and the (magic) string straight
        // across the forearm, square to the arm.
        Transform forearm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Vector3 armDown = forearm != null ? (leftHand.position - forearm.position).normalized : Vector3.down;
        Vector3 carryAlong = Vector3.ProjectOnPlane(forward, armDown).normalized;

        Vector3 up = Vector3.Slerp(carryAlong, aimUp, _raised).normalized;
        Vector3 face = Vector3.Slerp(armDown, _aim, _raised).normalized;
        transform.position = grip;

        Vector3 topTip = Limb(_top, grip, up, face);
        Vector3 bottomTip = Limb(_bottom, grip, -up, face);

        // The string: taken by the right hand's fingers while a shot is drawn (eased on, so it doesn't jump), otherwise
        // straight; just let go, it quivers back to straight.
        Vector3 rest = (topTip + bottomTip) * 0.5f;
        Vector3 held = fingers != null ? fingers.position : rest;
        _held = Mathf.MoveTowards(_held, _draw > 0f ? 1f : 0f, Time.deltaTime * 12f);
        if (_held > 0f) _twang = (held - rest) * _held;
        _twangAge += Time.deltaTime;
        Vector3 quiver = _twang * (Mathf.Cos(_twangAge * 70f) * Mathf.Exp(-_twangAge * 14f));
        Vector3 nock = _held > 0f ? Vector3.Lerp(rest, held, _held * _shown * _raised) : rest + quiver * _shown;
        _string.positionCount = 3;
        _string.SetPosition(0, topTip);
        _string.SetPosition(1, nock);
        _string.SetPosition(2, bottomTip);

        // The spirit arrow: from the string over the arrow rest (top right of the bow fist) and on past the bow, so it's
        // always on the right of the bow, wherever the string hand is in its draw.
        Vector3 arrowRest = grip + Vector3.Cross(Vector3.up, flatAim) * ArrowBeside + Vector3.up * ArrowAbove;
        Vector3 tip = arrowRest + (arrowRest - nock).normalized * ArrowReach;
        if ((arrowRest - nock).sqrMagnitude < 0.01f) tip = arrowRest + _aim * ArrowReach;
        _light.transform.position = tip; // lights the arrow, not the hand round the bow
        // A quick shot from the carry leaves from where the bow is going (chest height), not from his hip.
        Vector3 raisedTip = _player.position + Vector3.up * 1.35f + forward * 0.3f + _aim * ArrowReach;
        Nock = Vector3.Lerp(raisedTip, tip, _raised);
        _arrowShown = Mathf.MoveTowards(_arrowShown, _draw > 0f ? 1f : 0f, Time.deltaTime * 10f);
        _arrow.enabled = _arrowShown > 0.01f;
        if (_arrow.enabled)
        {
            _arrow.positionCount = 2;
            _arrow.SetPosition(0, nock);
            _arrow.SetPosition(1, tip);
            bool full = _draw >= 0.99f;
            float pulse = full ? 0.8f + 0.2f * Mathf.Sin(Time.time * 18f) : 1f;
            Color c = Color.Lerp(_arrowColour, Color.white, 0.25f + 0.35f * _draw) * (1f + 1.2f * _draw) * pulse;
            c.a = _arrowShown * Mathf.Lerp(0.35f, 1f, _draw) * _shown;
            _arrow.material.color = c;
            float width = Mathf.Lerp(0.6f, 1.2f, _draw);
            _arrow.startWidth = 0.025f * width;
            _arrow.endWidth = 0.07f * width;
        }

        UpdateCharge(tip);
    }

    // ---------------- The charge: motes, a tightening ring, a flare at the tip ----------------
    private void UpdateCharge(Vector3 tip)
    {
        _motes.transform.position = tip;
        _rings.transform.SetPositionAndRotation(tip + _aim * 0.25f, Quaternion.LookRotation(_aim));
        _tip.transform.position = tip;

        bool drawing = _draw > 0f && _shown > 0.5f;
        var emission = _motes.emission;
        emission.rateOverTime = drawing ? Mathf.Lerp(18f, 70f, _draw) : 0f;
        var main = _motes.main;
        main.startColor = Color.Lerp(_arrowColour, Color.white, 0.3f);

        var tipMain = _tip.main;
        tipMain.startColor = Color.Lerp(_arrowColour, Color.white, 0.4f + 0.4f * _draw);
        tipMain.startSize = Mathf.Lerp(0.12f, 0.32f, _draw);
        var tipEmission = _tip.emission;
        tipEmission.rateOverTime = drawing ? 24f : 0f;

        if (!drawing) { _wasFull = false; _ringTimer = 0f; return; }

        // A ring every so often, closing onto the line of the shot, quicker as the draw builds.
        _ringTimer -= Time.deltaTime;
        if (_ringTimer <= 0f)
        {
            _ringTimer = Mathf.Lerp(0.4f, 0.14f, _draw);
            _rings.Emit(new ParticleSystem.EmitParams
            {
                startSize = Mathf.Lerp(1.1f, 0.7f, _draw),
                startLifetime = Mathf.Lerp(0.4f, 0.25f, _draw),
                startColor = Color.Lerp(_arrowColour, Color.white, 0.3f * _draw),
            }, 1);
        }

        // Fully drawn: the tip flares once (and then pulses, above).
        bool full = _draw >= 0.99f;
        if (full && !_wasFull)
        {
            _tip.Emit(new ParticleSystem.EmitParams { startSize = 0.75f, startLifetime = 0.2f, startColor = Color.Lerp(_arrowColour, Color.white, 0.6f) }, 1);
            _rings.Emit(new ParticleSystem.EmitParams { startSize = 0.2f, startLifetime = 0.3f, startColor = Color.white }, 1);
        }
        _wasFull = full;
    }

    private void BuildCharge()
    {
        Material dot = Resources.Load<Material>(Folder + "ChargePulse_Dot");
        Material ring = Resources.Load<Material>(Folder + "ChargePulse_Ring");
        Material glow = Resources.Load<Material>(Folder + "ChargePulse_Glow");

        // Motes drawn in from a shell round the tip, spiralling as they come.
        _motes = System("Motes", dot, ParticleSystemSimulationSpace.Local, ParticleSystemRenderSpace.View);
        {
            var main = _motes.main;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            var shape = _motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            shape.radiusThickness = 0f;
            var velocity = _motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(-1.7f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(4f);
            _motes.Play();
        }

        // Rings that close onto the shot, standing across it (local alignment: they face along the aim).
        _rings = System("Rings", ring, ParticleSystemSimulationSpace.Local, ParticleSystemRenderSpace.Local);
        {
            var size = _rings.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.12f)));
            var emission = _rings.emission;
            emission.enabled = false;
        }

        // The glow at the tip, growing with the draw.
        _tip = System("Tip", glow, ParticleSystemSimulationSpace.Local, ParticleSystemRenderSpace.View);
        {
            var main = _tip.main;
            main.loop = true;
            main.startLifetime = 0.18f;
            var shape = _tip.shape;
            shape.enabled = false;
            _tip.Play();
        }
    }

    private ParticleSystem System(string name, Material material, ParticleSystemSimulationSpace space, ParticleSystemRenderSpace facing)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = space;
        main.startSpeed = 0f;
        main.maxParticles = 120;
        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(g);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = facing;
        renderer.sharedMaterial = material;
        return ps;
    }

    // One limb from the grip, curving out and its tip sweeping back towards the archer. Returns the tip.
    private Vector3 Limb(LineRenderer line, Vector3 grip, Vector3 along, Vector3 forward)
    {
        line.positionCount = LimbPoints;
        Vector3 tip = grip;
        for (int i = 0; i < LimbPoints; i++)
        {
            float t = i / (float)(LimbPoints - 1);
            // A bow's curve: out along the limb, forward a little in the middle, back at the tip.
            Vector3 p = grip + along * (Riser + LimbLength * t) + forward * (0.18f * Mathf.Sin(t * Mathf.PI) - LimbBack * t * t);
            line.SetPosition(i, p);
            tip = p;
        }
        return tip;
    }
}

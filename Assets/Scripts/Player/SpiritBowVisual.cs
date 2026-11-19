using UnityEngine;

/// <summary>
/// The Spirit Bow as you see it: two glowing limbs drawn from the left hand (top half in one gauntlet's colour,
/// bottom half in the other's when both bows are out), a string that pulls back towards the right hand as a shot is
/// drawn, and a little light. Placed every frame after the animation, from the hands and the way the player faces.
/// </summary>
public class SpiritBowVisual : MonoBehaviour
{
    private const int LimbPoints = 10;
    private const float LimbLength = 0.85f;  // hand to tip
    private const float LimbBack = 0.28f;    // how far the tips sweep back towards the archer
    private const float Glow = 1.35f;        // a little over 1 so the bloom picks it up; more washes the colour to white
    private const float Cant = 15f;          // degrees the bow leans, as archers hold it (and so it reads as a bow from behind)
    private const float RestDraw = 0.15f;    // the string sits a little drawn, so it never hides in the limbs

    private Animator _animator;
    private Transform _player;
    private LineRenderer _top, _bottom, _string;
    private Light _light;
    private Color _topColour, _bottomColour;
    private float _draw, _shown;

    /// <summary>Where an arrow leaves the bow.</summary>
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
            _light = new GameObject("Glow").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.type = LightType.Point;
            _light.range = 3.5f;
            _light.shadows = LightShadows.None;
        }
        Paint();
    }

    /// <summary>How far the string is drawn (0-1), and how much the bow is shown (0 = put away for a moment).</summary>
    public void SetDrawn(float draw, float shown)
    {
        _draw = draw;
        _shown = shown;
        Paint();
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
        _light.color = Color.Lerp(_topColour, _bottomColour, 0.5f);
        _light.intensity = 2.5f * _shown * (1f + _draw);
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
        Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (leftHand == null) return;

        Vector3 forward = _player.forward;
        Vector3 up = Quaternion.AngleAxis(-Cant, forward) * Vector3.up; // leaning the top away to the right
        Vector3 grip = leftHand.position + forward * 0.08f;
        transform.position = grip;

        Vector3 topTip = Limb(_top, grip, up, forward);
        Vector3 bottomTip = Limb(_bottom, grip, -up, forward);

        // The string: straight between the tips at rest, pulled back to the right hand as it's drawn.
        Vector3 rest = (topTip + bottomTip) * 0.5f;
        Vector3 pulled = rightHand != null ? rightHand.position : rest - forward * 0.5f;
        Vector3 nock = Vector3.Lerp(rest, pulled, Mathf.Max(RestDraw, _draw));
        _string.positionCount = 3;
        _string.SetPosition(0, topTip);
        _string.SetPosition(1, nock);
        _string.SetPosition(2, bottomTip);

        Nock = grip + forward * 0.25f;
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
            Vector3 p = grip + along * (LimbLength * t) + forward * (0.18f * Mathf.Sin(t * Mathf.PI) - LimbBack * t * t);
            line.SetPosition(i, p);
            tip = p;
        }
        return tip;
    }
}

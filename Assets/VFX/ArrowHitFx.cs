using UnityEngine;

/// <summary>
/// A Spirit Bow arrow striking something, laid out along the arrow's flight rather than on the floor:
///  - a flash where it struck,
///  - a shockwave ring standing across the line of flight (facing back up the arrow's path),
///  - sparks sprayed onward along the path, a few kicked back towards the archer,
///  - a short streak through the point of impact, as if the bolt's light carried on,
///  - a quick light.
/// Built in code from the charge-pulse materials, in the arrow's element colour. A heavy (fully drawn) hit is bigger,
/// with a second ring.
/// </summary>
public class ArrowHitFx : MonoBehaviour
{
    private const string Folder = "ChargePulse/";
    private const float Life = 0.9f;

    private Light _light;
    private float _age, _lightStart;

    /// <param name="direction">The way the arrow was flying.</param>
    /// <param name="heavy">A fully drawn arrow: bigger, with a second ring.</param>
    /// <param name="solid">It struck a wall rather than an enemy: no streak carrying on, sparks thrown back.</param>
    public static void Play(Vector3 at, Vector3 direction, Color colour, bool heavy, bool solid = false)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        var go = new GameObject("Arrow Hit");
        go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(direction.normalized));
        go.AddComponent<ArrowHitFx>().Build(colour, heavy, solid);
    }

    private void Build(Color colour, bool heavy, bool solid)
    {
        Material dot = Resources.Load<Material>(Folder + "ChargePulse_Dot");
        Material ring = Resources.Load<Material>(Folder + "ChargePulse_Ring");
        Material glow = Resources.Load<Material>(Folder + "ChargePulse_Glow");
        float scale = heavy ? 1.6f : 1f;
        Color hot = Color.Lerp(colour, Color.white, 0.55f);
        Vector3 forward = Vector3.forward; // the systems simulate in this object's space, which faces along the flight

        // The flash.
        ParticleSystem flash = System("Flash", glow, ParticleSystemRenderSpace.View);
        flash.Emit(new ParticleSystem.EmitParams { startSize = 0.9f * scale, startLifetime = 0.12f, startColor = hot }, 1);
        flash.Emit(new ParticleSystem.EmitParams { startSize = 0.5f * scale, startLifetime = 0.22f, startColor = colour }, 1);

        // Shockwave rings across the line of flight: billboards turned to face along the arrow (local alignment).
        ParticleSystem rings = System("Rings", ring, ParticleSystemRenderSpace.Local);
        Grow(rings, 0.2f, 1f);
        rings.Emit(new ParticleSystem.EmitParams { startSize = 1.3f * scale, startLifetime = 0.28f, startColor = hot }, 1);
        if (heavy)
        {
            rings.Emit(new ParticleSystem.EmitParams
            {
                position = forward * 0.6f, startSize = 2.2f * scale, startLifetime = 0.4f, startColor = colour,
            }, 1);
        }

        // Sparks: streaks sprayed on along the path (or back off a wall), and a few kicked back.
        ParticleSystem sparks = System("Sparks", dot, ParticleSystemRenderSpace.View);
        var renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.045f;
        renderer.lengthScale = 1.5f;
        var gravity = sparks.main;
        gravity.gravityModifier = 0.6f;
        int onward = Mathf.RoundToInt((solid ? 6 : 14) * scale);
        int back = Mathf.RoundToInt((solid ? 12 : 5) * scale);
        for (int i = 0; i < onward + back; i++)
        {
            bool isBack = i >= onward;
            Vector3 dir = Cone(isBack ? -forward : forward, isBack ? 55f : 28f);
            sparks.Emit(new ParticleSystem.EmitParams
            {
                velocity = dir * Random.Range(5f, 11f) * (isBack ? 0.6f : 1f) * Mathf.Sqrt(scale),
                startSize = Random.Range(0.04f, 0.08f) * scale,
                startLifetime = Random.Range(0.14f, 0.3f),
                startColor = Color.Lerp(colour, Color.white, Random.value * 0.6f),
            }, 1);
        }

        // The streak: the bolt's light carrying on through the target.
        if (!solid)
        {
            var streak = new GameObject("Streak").AddComponent<LineRenderer>();
            streak.transform.SetParent(transform, false);
            streak.useWorldSpace = false;
            streak.positionCount = 2;
            streak.SetPosition(0, Vector3.back * 0.4f);
            streak.SetPosition(1, Vector3.forward * (heavy ? 2.2f : 1.1f));
            streak.startWidth = 0.14f * scale;
            streak.endWidth = 0f;
            streak.material = new Material(Shader.Find("Sprites/Default")) { color = colour * 1.6f };
            streak.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _streak = streak;
        }

        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = colour;
        _light.range = 3f * scale;
        _light.shadows = LightShadows.None;
        _lightStart = 4f * scale;
        _light.intensity = _lightStart;
    }

    private LineRenderer _streak;

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= Life) { Destroy(gameObject); return; }

        float u = _age / 0.18f;
        _light.intensity = _lightStart * Mathf.Clamp01(1f - u);
        if (_streak != null)
        {
            // Shoots on and fades in a tenth of a second.
            float s = Mathf.Clamp01(_age / 0.12f);
            Color c = _streak.material.color;
            c.a = 1f - s;
            _streak.material.color = c;
            _streak.transform.localPosition = Vector3.forward * (s * 0.8f);
        }
    }

    private static Vector3 Cone(Vector3 axis, float degrees)
    {
        Vector2 r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
        return (Quaternion.LookRotation(axis) * new Vector3(r.x, r.y, 1f)).normalized;
    }

    private ParticleSystem System(string name, Material material, ParticleSystemRenderSpace facing)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // AddComponent auto-plays

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0f;
        main.maxParticles = 64;
        var emission = ps.emission;
        emission.enabled = false;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(g);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = facing;
        renderer.sharedMaterial = material;
        return ps;
    }

    private static void Grow(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from, 0f, 3f), new Keyframe(1f, to)));
    }
}

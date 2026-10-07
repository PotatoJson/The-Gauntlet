using UnityEngine;

/// <summary>
/// The heavy-attack charge effect on the player's fist: rings pulse inward and focus on the glove, sparks
/// stream into it and a core glow grows as the charge fills, then a burst goes off on release.
/// Built from code (no prefab to wire up) and parented to the striking hitbox, so it follows the fist.
/// Colour follows the gauntlet's element. Drive it with Begin / SetCharge / Release / Cancel.
/// </summary>
public class ChargePulseVFX : MonoBehaviour
{
    private const string Folder = "ChargePulse/";

    private ParticleSystem _converge, _rings, _core, _burstRing, _burstSparks;
    private Color _color = Color.white;
    private float _charge;
    private float _ringTimer;
    private bool _active;

    public bool IsActive => _active;

    /// <summary>Creates the effect on a fist and parks it (nothing plays until Begin).</summary>
    public static ChargePulseVFX Create(Transform fist)
    {
        var go = new GameObject("ChargePulseVFX");
        go.transform.SetParent(fist, false);

        // The rig is scaled up hard (the glove is ~50x in world space); cancel that so sizes below are world units.
        Vector3 s = fist.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));

        var vfx = go.AddComponent<ChargePulseVFX>();
        vfx.Build();
        return vfx;
    }

    public static Color ElementColor(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return new Color(1f, 0.45f, 0.1f);
            case ElementType.Ice: return new Color(0.2f, 0.7f, 1f);
            case ElementType.Earth: return new Color(0.75f, 0.5f, 0.2f);
            case ElementType.Lightning: return new Color(1f, 0.95f, 0.3f);
            case ElementType.Wind: return new Color(0.6f, 1f, 0.9f);
            default: return new Color(1f, 0.9f, 0.7f);
        }
    }

    public void Begin(Color color)
    {
        _color = color;
        _charge = 0f;
        _ringTimer = 0f;
        _active = true;

        foreach (ParticleSystem ps in new[] { _converge, _rings, _core })
        {
            var main = ps.main;
            main.startColor = color;
        }

        _converge.Play();
        _core.Play();
        ApplyCharge();
    }

    /// <summary>0 = just started, 1 = fully charged.</summary>
    public void SetCharge(float charge)
    {
        if (!_active) return;
        _charge = Mathf.Clamp01(charge);
        ApplyCharge();
    }

    /// <summary>The swing is released: stop charging and go off.</summary>
    public void Release()
    {
        if (!_active) return;
        StopCharging();

        float power = Mathf.Lerp(0.6f, 1.2f, _charge);

        var ring = new ParticleSystem.EmitParams { startSize = 1.0f * power, startLifetime = 0.28f, startColor = Color.Lerp(_color, Color.white, 0.5f) };
        _burstRing.Emit(ring, 1);

        int sparks = Mathf.RoundToInt(Mathf.Lerp(8, 18, _charge));
        for (int i = 0; i < sparks; i++)
        {
            var e = new ParticleSystem.EmitParams
            {
                velocity = Random.onUnitSphere * Random.Range(1.6f, 3.4f) * power,
                startLifetime = Random.Range(0.18f, 0.34f),
                startSize = Random.Range(0.03f, 0.06f),
                startColor = Color.Lerp(_color, Color.white, Random.value * 0.6f)
            };
            _burstSparks.Emit(e, 1);
        }
    }

    /// <summary>The charge was interrupted: just fade out, no burst.</summary>
    public void Cancel()
    {
        if (!_active) return;
        StopCharging();
    }

    private void StopCharging()
    {
        _active = false;
        _converge.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        _core.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    private void Update()
    {
        if (!_active) return;

        // Focusing rings: one every so often, faster as the charge builds.
        _ringTimer -= Time.deltaTime;
        if (_ringTimer <= 0f)
        {
            _ringTimer = Mathf.Lerp(0.55f, 0.2f, _charge);

            var e = new ParticleSystem.EmitParams
            {
                startSize = Mathf.Lerp(0.6f, 0.85f, _charge),
                startLifetime = Mathf.Lerp(0.45f, 0.3f, _charge),
                startColor = Color.Lerp(_color, Color.white, _charge * 0.5f)
            };
            _rings.Emit(e, 1);
        }
    }

    private void ApplyCharge()
    {
        var conv = _converge.emission;
        conv.rateOverTime = Mathf.Lerp(10f, 45f, _charge);

        var core = _core.main;
        core.startSize = Mathf.Lerp(0.1f, 0.3f, _charge);
        core.startColor = Color.Lerp(_color, Color.white, _charge * 0.6f);
    }

    // ---------------------------------------------------------------- building

    private void Build()
    {
        Material add = Resources.Load<Material>(Folder + "ChargePulse_Dot");
        Material ringMat = Resources.Load<Material>(Folder + "ChargePulse_Ring");
        Material glowMat = Resources.Load<Material>(Folder + "ChargePulse_Glow");

        // Sparks stream into the fist from a shell around it.
        _converge = NewSystem("Converge", add, loop: true, local: true);
        {
            var main = _converge.main;
            main.startLifetime = 0.28f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            var shape = _converge.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            shape.radiusThickness = 0f; // emit from the surface only
            var vel = _converge.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(-1.3f);
            var fade = _converge.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeInOut();
        }

        // Rings that shrink onto the fist ("focusing").
        _rings = NewSystem("Rings", ringMat, loop: false, local: true);
        {
            var main = _rings.main;
            main.startSpeed = 0f;
            var emission = _rings.emission;
            emission.enabled = false; // emitted by hand from Update
            var size = _rings.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)));
            var fade = _rings.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeInOut();
        }

        // Core glow that grows with the charge.
        _core = NewSystem("Core", glowMat, loop: true, local: true);
        {
            var main = _core.main;
            main.startLifetime = 0.2f;
            main.startSpeed = 0f;
            var emission = _core.emission;
            emission.rateOverTime = 14f;
            var shape = _core.shape;
            shape.enabled = false;
            var fade = _core.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeInOut();
        }

        // Release: one ring thrown outward, plus sparks.
        _burstRing = NewSystem("BurstRing", ringMat, loop: false, local: false);
        {
            var main = _burstRing.main;
            main.startSpeed = 0f;
            var emission = _burstRing.emission;
            emission.enabled = false;
            var size = _burstRing.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1f)));
            var fade = _burstRing.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeOut();
        }

        _burstSparks = NewSystem("BurstSparks", add, loop: false, local: false);
        {
            var main = _burstSparks.main;
            main.startSpeed = 0f;
            var emission = _burstSparks.emission;
            emission.enabled = false;
            var fade = _burstSparks.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeOut();
        }
    }

    private ParticleSystem NewSystem(string name, Material material, bool loop, bool local)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // AddComponent auto-plays

        var main = ps.main;
        main.loop = loop;
        main.playOnAwake = false;
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.startLifetime = 0.3f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingFudge = -2f; // draw over the glove

        return ps;
    }

    private static ParticleSystem.MinMaxGradient FadeInOut()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static ParticleSystem.MinMaxGradient FadeOut()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }
}

using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The Hell Lord's entrance. A dense cloud fills the arena, then blows away to show the untextured statue, which paints
/// itself with its real texture and fires its beam at the player, who rolls clear in slow motion. A fade hands control
/// back from there.
/// </summary>
public class HellLordIntro : BossIntroCutscene
{
    private const string AuraPrefabName = "HellAuraFx";
    private const string IdleState = "HumanM@Idle01";
    private static readonly int AnimBlast = Animator.StringToHash("Blast");

    private const float FadeFromBlackSeconds = 0.9f;
    private const float FogHoldSeconds = 1.6f;
    private const float FogClearSeconds = 2.4f;
    private const float PaintSeconds = 3f;
    private const float RoarLeadSeconds = 0.5f;

    // Measured on the blast clip's own clock, so that slowing time slows them with the animation.
    private const float FireTime = 1.79f;       // the moment the clip thrusts the beam out
    private const float BeamEndTime = 2.46f;
    private const float DodgeStartTime = 1.62f;
    private const float DodgeSeconds = 0.62f;
    private const float SlowMoStartTime = 1.45f;
    private const float SlowMoEndTime = 2.2f;
    private const float SlowMoScale = 0.3f;

    private const float DodgeDistance = 4.5f;
    private const float ShoulderBack = 7.5f;
    private const float ShoulderSide = 2.5f;
    private const float ShoulderUp = 1.8f;
    private const int FogPuffs = 170;

    [SerializeField] private Material fogMaterial;
    [SerializeField] private Color fogColor = new Color(0.42f, 0.38f, 0.46f, 0.6f);

    private ParticleSystem _fog;
    private ParticleSystem _aura;
    private GameObject _beam;

    protected override void OnBegin()
    {
        float height = BossHeight();
        _paintCenter = _bossHome + Vector3.up * height * 0.9f;

        // The player was only just stepping off the ledge: black covers the jump to standing on the floor.
        SetFade(1f);
        SetBars(1f);
        _player.position = _landing;
        _player.rotation = Quaternion.LookRotation(_toBoss);
        _playerAnimator.CrossFadeInFixedTime("Locomotion", 0f);

        _fog = CreateFog();
    }

    protected override void RestoreScene()
    {
        Time.timeScale = 1f;

        if (_fog != null) Destroy(_fog.gameObject);
        if (_beam != null) Destroy(_beam);
        if (_aura != null) Destroy(_aura.gameObject);

        _bossAnimator.ResetTrigger(AnimBlast);
        _bossAnimator.Play(IdleState, 0, 0f);
    }

    protected override IEnumerator Timeline()
    {
        float height = BossHeight();
        Vector3 chest = _bossHome + Vector3.up * height * 0.6f;
        Vector3 head = _bossHome + Vector3.up * height * 0.9f;
        Func<Vector3> body = () => chest;
        Func<Vector3> face = () => head;

        // From behind the player, into the cloud, drifting forward as it clears.
        Vector3 behind = _landing - _toBoss * 4.5f + _side * 1.3f + Vector3.up * 0.9f;
        Vector3 forward = behind + _toBoss * 3f;
        float cloudSeconds = FogHoldSeconds + FogClearSeconds;
        Coroutine drift = StartCoroutine(Shot(behind, forward, body, body, cloudSeconds));
        yield return Fade(1f, 0f, FadeFromBlackSeconds);
        StoryDialogue.Say("Story_A2_Boss_01");
        yield return new WaitForSecondsRealtime(FogHoldSeconds - FadeFromBlackSeconds);

        StartCoroutine(ClearFog(FogClearSeconds));
        yield return new WaitForSecondsRealtime(FogClearSeconds);
        StopCoroutine(drift);

        // Close on the statue as the texture spreads down from its head.
        Vector3 close = _bossHome - _toBoss * height * 1.1f + _side * 1.2f + Vector3.up * height * 0.75f;
        Vector3 full = _bossHome - _toBoss * height * 2.2f + _side * 3.5f + Vector3.up * height * 0.45f;
        _bossAnimator.speed = 1f;
        StartCoroutine(Paint(PaintSeconds));
        StoryDialogue.Say("Story_A2_Boss_02", Speaker.Pergius, 1f);
        yield return Shot(close, full, face, body, PaintSeconds - RoarLeadSeconds);

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Roar", _bossHome);
        yield return new WaitForSecondsRealtime(RoarLeadSeconds);

        // Over the player's shoulder, looking at the boss. The player rolls away from the camera's side, so the roll
        // stays in frame and doesn't block the view. The aim sits a little towards the roll to keep them both in shot.
        Vector3 dodge = DodgeDirection();
        Vector3 overShoulder = _landing - _toBoss * ShoulderBack - dodge * ShoulderSide + Vector3.up * ShoulderUp;
        Vector3 aim = Vector3.Lerp(_landing, _bossHome, 0.5f) + dodge * 2.5f + Vector3.up * 0.8f;
        Func<Vector3> beamView = () => aim;
        float beamSeconds = SlowMoStartTime + (SlowMoEndTime - SlowMoStartTime) / SlowMoScale + (BeamEndTime - SlowMoEndTime);
        drift = StartCoroutine(Shot(overShoulder, overShoulder + _toBoss * 0.8f, beamView, beamView, beamSeconds + 0.4f));
        StoryDialogue.Say("Story_A2_Boss_03", Speaker.Pergius, 1f);
        yield return BeamAndDodge(dodge);
        yield return new WaitForSecondsRealtime(0.4f);
        StopCoroutine(drift);

        // Fade out, and fade back in on the player.
        yield return Fade(0f, 1f, FadeOutSeconds);
        yield return HandBack();
    }

    private IEnumerator BeamAndDodge(Vector3 dodge)
    {
        HellLord lord = GetComponent<HellLord>();

        EnemyFx.Impact(_bossHome, 3f, EnemyFx.Hell);
        _aura = EnemyFx.Fury(transform, AuraPrefabName);
        _bossAnimator.SetTrigger(AnimBlast);

        // Aimed at the ground under the player, where it visibly strikes: the roll is what makes it miss.
        Vector3 target = EnemyFx.FloorUnder(_landing);
        Vector3 dodgeStart = _landing;
        Vector3 dodgeEnd = StandingAt(_landing + dodge * DodgeDistance);

        float clock = 0f;
        float dodging = 0f;
        bool slowed = false, rolled = false, fired = false, sped = false;

        while (clock < BeamEndTime)
        {
            clock += Time.deltaTime;

            if (!slowed && clock >= SlowMoStartTime)
            {
                slowed = true;
                Time.timeScale = SlowMoScale;
            }

            if (!rolled && clock >= DodgeStartTime)
            {
                rolled = true;
                _landing = dodgeEnd; // a skip from here on must leave the player clear too
                _player.rotation = Quaternion.LookRotation(dodgeEnd - dodgeStart);
                _playerAnimator.CrossFadeInFixedTime("Roll", 0.05f);
                FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Roll", dodgeStart);
            }

            if (rolled && dodging < DodgeSeconds)
            {
                dodging += Time.deltaTime;
                _player.position = Vector3.Lerp(dodgeStart, dodgeEnd, Mathf.SmoothStep(0f, 1f, dodging / DodgeSeconds));
                if (dodging >= DodgeSeconds)
                {
                    _player.rotation = Quaternion.LookRotation(_toBoss);
                    _playerAnimator.CrossFadeInFixedTime("Locomotion", 0.2f);
                }
            }

            if (!fired && clock >= FireTime)
            {
                fired = true;
                FireBeam(lord, target);
            }

            if (_beam != null) AimBeam(lord, target);

            if (!sped && clock >= SlowMoEndTime)
            {
                sped = true;
                Time.timeScale = 1f;
                _cameraShake = 0f;
            }

            yield return null;
        }

        if (_beam != null) Destroy(_beam);
    }

    private void FireBeam(HellLord lord, Vector3 target)
    {
        _beam = Instantiate(lord.BeamPrefab, lord.BeamSpawnPoint.position, Quaternion.identity);
        AimBeam(lord, target);

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/HellLord_Beam", _bossHome);
        EnemyFx.Impact(target, 3f, EnemyFx.Hell);
        EnemyFx.GroundCrack(target, 2.6f, EnemyFx.Hell);
        _cameraShake = 0.1f;
    }

    private void AimBeam(HellLord lord, Vector3 target)
    {
        Vector3 from = lord.BeamSpawnPoint.position;
        _beam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));
    }

    // Whichever side has room to roll into.
    private Vector3 DodgeDirection()
    {
        bool clear = UnityEngine.AI.NavMesh.SamplePosition(_landing, out UnityEngine.AI.NavMeshHit start, 2f, UnityEngine.AI.NavMesh.AllAreas)
            && !UnityEngine.AI.NavMesh.Raycast(start.position, start.position + _side * DodgeDistance, out _, UnityEngine.AI.NavMesh.AllAreas);
        return clear ? _side : -_side;
    }

    #region Fog
    private ParticleSystem CreateFog()
    {
        GameObject host = new GameObject("HellLordIntroFog");
        ParticleSystem fog = host.AddComponent<ParticleSystem>();
        fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = fog.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = FogPuffs;

        ParticleSystem.EmissionModule emission = fog.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = fog.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = fog.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = fogMaterial;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;

        // A long box from behind the camera's first position to past the boss.
        Vector3 middle = (_bossHome + _landing) * 0.5f;
        float halfLength = Vector3.Distance(_bossHome, _landing) * 0.5f + 12f;

        for (int i = 0; i < FogPuffs; i++)
        {
            Vector3 position = middle
                + _toBoss * UnityEngine.Random.Range(-halfLength, halfLength)
                + _side * UnityEngine.Random.Range(-16f, 16f)
                + Vector3.up * UnityEngine.Random.Range(0f, 8f);

            ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
            {
                position = position,
                startSize = UnityEngine.Random.Range(9f, 17f),
                startLifetime = 120f,
                startColor = fogColor,
                rotation = UnityEngine.Random.Range(0f, 360f),
                velocity = UnityEngine.Random.insideUnitSphere * 0.25f
            };
            fog.Emit(puff, 1);
        }

        return fog;
    }

    // Each puff is blown outwards from the middle of the arena, thinning as it goes.
    private IEnumerator ClearFog(float seconds)
    {
        ParticleSystem.Particle[] puffs = new ParticleSystem.Particle[FogPuffs];
        int count = _fog.GetParticles(puffs);
        Vector3 middle = (_bossHome + _landing) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector3 outwards = puffs[i].position - middle;
            outwards.y = Mathf.Abs(outwards.y) * 0.3f;
            puffs[i].velocity = outwards.normalized * UnityEngine.Random.Range(5f, 11f);
        }

        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            Color color = fogColor;
            color.a *= 1f - Mathf.SmoothStep(0f, 1f, elapsed / seconds);

            count = Mathf.Min(count, _fog.particleCount);
            for (int i = 0; i < count; i++) puffs[i].startColor = color;

            _fog.SetParticles(puffs, count);
            yield return null;
            _fog.GetParticles(puffs, count);
        }

        Destroy(_fog.gameObject);
    }
    #endregion
}

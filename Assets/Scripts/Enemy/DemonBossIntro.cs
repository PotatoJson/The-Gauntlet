using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The first boss's entrance: the player rolls down and lands, looks around as the torches die, and the torches relight
/// towards the statue. It shakes, its eyes grow, its real texture spreads over it, and the camera pulls out to frame
/// the two of them before the fade back to the player.
/// </summary>
public class DemonBossIntro : BossIntroCutscene
{
    private const float FlightSeconds = 1.15f;
    private const int Tumbles = 2;
    private const float LookAroundSeconds = 3.6f;
    private const float LightsOutSeconds = 2.6f;
    private const float DarkSeconds = 0.7f;
    private const float FirstTorchGap = 0.33f; // the torches relight slowly at first...
    private const float LastTorchGap = 0.03f;  // ...and this close together by the end
    private const float RoomLightFadeSeconds = 0.8f;
    private const float DarkClay = 0.15f;      // his unlit clay would otherwise stay bright in the dark
    private const float ShakeSeconds = 1.4f;
    private const float EyeSeconds = 1.3f;
    private const float PaintSeconds = 2.8f;
    private const float WideSeconds = 2.6f;
    private const float LookAngle = 65f;
    private const float OrbitRadius = 5.5f;
    private static readonly Color LandingDust = new Color(0.55f, 0.5f, 0.42f, 1f);

    [SerializeField, Tooltip("The scene object holding this room's torches; every light under it goes out and comes back.")]
    private string roomName = "Boss Chamber";
    [SerializeField] private Material eyeMaterial;

    [Header("Eyes")]
    [SerializeField, Tooltip("From his head bone, in his own axes: x to each side, y up, z forward.")]
    private Vector3 eyeOffset = new Vector3(0.17f, 0.12f, 0.55f);
    [SerializeField, Tooltip("Full size of each eye: width, height, depth.")]
    private Vector3 eyeShape = new Vector3(0.3f, 0.05f, 0.1f);
    [SerializeField, Tooltip("Degrees each eye tilts, inner corner down, for a glare.")]
    private float eyeSlant = 18f;

    private Transform _eyeLeft, _eyeRight;

    private readonly List<Torch> _torches = new List<Torch>();
    private Light _roomLight;
    private float _roomLightIntensity;

    private Transform _playerHead;
    private float _headYaw;

    protected override void OnBegin()
    {
        _playerHead = _playerAnimator.GetBoneTransform(HumanBodyBones.Head);
        FindTorches();
    }

    protected override void RestoreScene()
    {
        AllLightsOn();
        _headYaw = 0f;

        if (_eyeLeft != null) Destroy(_eyeLeft.gameObject);
        if (_eyeRight != null) Destroy(_eyeRight.gameObject);
    }

    #region Torches
    private class Torch
    {
        public GameObject light; // carries the flicker and the lens flare too
        public Vector3 position;
        public Renderer flame;
        public Material[] litMaterials;
        public Material[] outMaterials;

        public void Set(bool lit)
        {
            light.SetActive(lit);
            if (flame != null) flame.sharedMaterials = lit ? litMaterials : outMaterials;
        }
    }

    private void FindTorches()
    {
        GameObject room = GameObject.Find(roomName);
        if (room == null) return;

        foreach (Light found in room.GetComponentsInChildren<Light>())
        {
            // The one light directly under the room is the big fill light, not a torch.
            if (found.transform.parent == room.transform)
            {
                _roomLight = found;
                _roomLightIntensity = found.intensity;
                continue;
            }

            Torch torch = new Torch { light = found.gameObject, position = found.transform.position };
            torch.flame = found.transform.parent.GetComponentInChildren<MeshRenderer>();
            if (torch.flame != null)
            {
                torch.litMaterials = torch.flame.sharedMaterials;
                torch.outMaterials = BurntOut(torch.litMaterials);
            }
            _torches.Add(torch);
        }

        // Nearest the player first, flat, so that the torches higher on the walls don't count as further away.
        Vector3 flat = new Vector3(1f, 0f, 1f);
        _torches.Sort((a, b) => Vector3.Scale(a.position - _landing, flat).sqrMagnitude
            .CompareTo(Vector3.Scale(b.position - _landing, flat).sqrMagnitude));
    }

    // The same materials with the glowing one swapped for one of the others on the torch.
    private static Material[] BurntOut(Material[] lit)
    {
        Material dark = Array.Find(lit, material => !material.IsKeywordEnabled("_EMISSION"));
        return Array.ConvertAll(lit, material => material.IsKeywordEnabled("_EMISSION") && dark != null ? dark : material);
    }

    // They die from the far end of the room, so the dark closes in on the player.
    private IEnumerator LightsOut(float seconds)
    {
        if (_roomLight != null) _roomLight.gameObject.SetActive(false);

        float gap = seconds / Mathf.Max(1, _torches.Count);
        float wait = 0f;
        for (int i = _torches.Count - 1; i >= 0; i--)
        {
            _torches[i].Set(false);
            SetClayBrightness(Mathf.Lerp(DarkClay, 1f, i / Mathf.Max(1f, _torches.Count)));

            wait += gap;
            while (wait > 0f)
            {
                yield return null;
                wait -= Time.unscaledDeltaTime;
            }
        }

        SetClayBrightness(DarkClay);
    }

    private float TorchGap(int index)
    {
        // Eases rather than running in a straight line, so that the rush only builds up in the second half.
        float progress = index / Mathf.Max(1f, _torches.Count - 1);
        return Mathf.Lerp(FirstTorchGap, LastTorchGap, Mathf.Pow(progress, 0.6f));
    }

    private float LightsOnSeconds()
    {
        float total = RoomLightFadeSeconds;
        for (int i = 0; i < _torches.Count; i++) total += TorchGap(i);
        return total;
    }

    // Back on from the player outwards, one by one and faster and faster, until they reach the boss.
    private IEnumerator LightsOn()
    {
        float wait = 0f;
        for (int i = 0; i < _torches.Count; i++)
        {
            _torches[i].Set(true);
            EnemyFx.Strike(_torches[i].position, 0.7f, EnemyFx.Fire);
            SetClayBrightness(Mathf.Lerp(DarkClay, 1f, Mathf.Pow(i / Mathf.Max(1f, _torches.Count - 1), 2f)));

            wait += TorchGap(i);
            while (wait > 0f)
            {
                yield return null;
                wait -= Time.unscaledDeltaTime;
            }
        }

        SetClayBrightness(1f);

        if (_roomLight == null) yield break;

        _roomLight.gameObject.SetActive(true);
        for (float elapsed = 0f; elapsed < RoomLightFadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            _roomLight.intensity = Mathf.Lerp(0f, _roomLightIntensity, elapsed / RoomLightFadeSeconds);
            yield return null;
        }
        _roomLight.intensity = _roomLightIntensity;
    }

    private void AllLightsOn()
    {
        foreach (Torch torch in _torches) torch.Set(true);

        if (_roomLight == null) return;
        _roomLight.gameObject.SetActive(true);
        _roomLight.intensity = _roomLightIntensity;
    }

    // Left, right and back to the front. The body turns a little and the head does the rest.
    private IEnumerator LookAround(float seconds)
    {
        Quaternion facing = Quaternion.LookRotation(_toBoss);

        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float yaw = Mathf.Sin(elapsed / seconds * Mathf.PI * 2f) * LookAngle;
            _player.rotation = facing * Quaternion.Euler(0f, yaw * 0.45f, 0f);
            _headYaw = yaw * 0.55f;
            yield return null;
        }

        _player.rotation = facing;
        _headYaw = 0f;
    }
    #endregion

    #region Timeline
    protected override IEnumerator Timeline()
    {
        StartCoroutine(Bars(0f, 1f, 0.5f));

        // The player rolls down and lands, seen from the platform.
        Vector3 ground = _landing + _toBoss * 4.5f + _side * 2.2f + Vector3.down * 0.3f;
        StartCoroutine(RollDown());
        Func<Vector3> player = () => _player.position + Vector3.up * 0.3f;
        yield return Shot(ground, ground + Vector3.up * 0.3f, player, player, FlightSeconds + 0.5f);

        // Close on the player, who looks around as the torches go out.
        Func<Vector3> playerHead = () => _player.position + Vector3.up * 0.65f;
        Vector3 closeUp = _landing + _toBoss * 3f + _side * 1.1f + Vector3.up * 0.5f;
        StartCoroutine(LookAround(LookAroundSeconds));
        StartCoroutine(LightsOut(LightsOutSeconds));
        yield return Shot(closeUp, closeUp + _toBoss * -0.6f, playerHead, playerHead, LookAroundSeconds);
        yield return new WaitForSecondsRealtime(DarkSeconds);

        // The torches relight while the camera circles the player, keeping up with them: from facing the ledge,
        // where the nearest ones are, past the side walls, to facing the boss as the last ones reach him.
        StartCoroutine(LightsOn());
        yield return OrbitPlayer(LightsOnSeconds());

        // Cut to the statue, which starts to shake.
        Vector3 chest = _bossHome + Vector3.up * 3.3f;
        Func<Vector3> body = () => chest;
        Vector3 front = _bossHome - _toBoss * 11f + _side * 2.5f + Vector3.up * 2f;
        Vector3 closer = _bossHome - _toBoss * 8.5f + _side * 1.8f + Vector3.up * 2.4f;
        _bossAnimator.speed = 1f;
        StartCoroutine(ShakeBoss(ShakeSeconds));
        yield return Shot(front, closer, body, body, ShakeSeconds);

        // Push in on the face as the eyes grow.
        Transform head = _bossAnimator.GetBoneTransform(HumanBodyBones.Head);
        Func<Vector3> face = () => head.position + Vector3.up * 0.2f;
        Vector3 faceShot = _bossHome - _toBoss * 4.5f + _side * 0.6f + Vector3.up * 4.8f;
        CreateEyes(head);
        StartCoroutine(GrowEyes(EyeSeconds));
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Roar", _bossHome);
        _cameraShake = 0.05f;
        yield return Shot(closer, faceShot, body, face, EyeSeconds);

        // The texture spreads out from the eyes while the camera backs away down his body.
        Vector3 fullBody = _bossHome - _toBoss * 11f + _side * 4f + Vector3.up * 2.4f;
        _cameraShake = 0f;
        StartCoroutine(Paint(PaintSeconds));
        yield return Shot(faceShot, fullBody, face, body, PaintSeconds);

        // Zoom out to the two of them.
        Vector3 middle = (_bossHome + _landing) * 0.5f;
        Func<Vector3> both = () => middle + Vector3.up * 2.5f;
        Vector3 wideStart = middle + _side * 13f + Vector3.up * 3.5f;
        Vector3 wideEnd = middle + _side * 19f + Vector3.up * 6.5f;
        yield return Shot(fullBody, wideStart, body, both, 0.9f);
        yield return Shot(wideStart, wideEnd, both, both, WideSeconds - 0.9f);

        // Fade out, and fade back in on the player.
        yield return Fade(0f, 1f, FadeOutSeconds);
        yield return HandBack();
    }

    private IEnumerator OrbitPlayer(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float angle = Mathf.SmoothStep(0f, 180f, elapsed / seconds);
            Vector3 fromPlayer = Quaternion.AngleAxis(angle, Vector3.up) * _toBoss;

            _shotPosition = _landing + fromPlayer * OrbitRadius + Vector3.up * 1.5f;
            _shotLook = _landing - fromPlayer * 15f + Vector3.up * 3.5f; // past the player, up at the torches
            yield return null;
        }
    }

    protected override void LateUpdate()
    {
        // Here because the animator has only just posed the head for this frame.
        if (_playing && _headYaw != 0f && _playerHead != null)
        {
            _playerHead.rotation = Quaternion.AngleAxis(_headYaw, Vector3.up) * _playerHead.rotation;
        }

        base.LateUpdate();
    }

    private IEnumerator RollDown()
    {
        Vector3 start = _player.position;
        _playerAnimator.CrossFadeInFixedTime("Roll", 0.08f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Roll", start);

        Quaternion facing = Quaternion.LookRotation(_toBoss);

        for (float elapsed = 0f; elapsed < FlightSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / FlightSeconds;
            Vector3 position = Vector3.Lerp(start, _landing, t);
            position.y = Mathf.Lerp(start.y, _landing.y, t * t); // falling: slow at the top, fast at the bottom
            _player.position = position;

            // The roll clip is made for the ground, so the somersault through the air is added here.
            _player.rotation = facing * Quaternion.Euler(360f * Tumbles * Mathf.SmoothStep(0f, 1f, t), 0f, 0f);

            yield return null;
        }

        _player.position = _landing;
        _player.rotation = facing;
        _playerAnimator.CrossFadeInFixedTime("Locomotion", 0.25f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Land", _landing);
        EnemyFx.Impact(_landing, 1.3f, LandingDust);

        _cameraShake = 0.08f;
        yield return new WaitForSecondsRealtime(0.18f);
        _cameraShake = 0f;
    }

    private IEnumerator ShakeBoss(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float strength = Mathf.Lerp(0.015f, 0.09f, elapsed / seconds);
            Vector3 offset = UnityEngine.Random.insideUnitSphere * strength;
            offset.y *= 0.3f;
            transform.position = _bossHome + offset;
            yield return null;
        }

        transform.position = _bossHome;
    }

    private void CreateEyes(Transform head)
    {
        _eyeLeft = CreateEye(head, -1f);
        _eyeRight = CreateEye(head, 1f);

        _paintCenter = (_eyeLeft.position + _eyeRight.position) * 0.5f;
    }

    private Transform CreateEye(Transform head, float side)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(eye.GetComponent<Collider>());
        eye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;

        Vector3 local = new Vector3(eyeOffset.x * side, eyeOffset.y, eyeOffset.z);
        eye.transform.position = head.position + transform.right * local.x + transform.up * local.y + transform.forward * local.z;
        eye.transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, eyeSlant * side);
        eye.transform.SetParent(head, true);
        eye.transform.localScale = Vector3.zero;
        return eye.transform;
    }

    private IEnumerator GrowEyes(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            // Ease out with a small overshoot, so that they swell open rather than scale up.
            float t = elapsed / seconds;
            float size = 1f + 1.4f * Mathf.Pow(t - 1f, 3f) + 0.4f * Mathf.Pow(t - 1f, 2f);
            SetEyeSize(Mathf.Max(0f, size));
            yield return null;
        }

        SetEyeSize(1f);
    }

    private void SetEyeSize(float amount)
    {
        // The head bone may be scaled, and the eyes should keep their shape in the world.
        Vector3 scale = eyeShape * amount / _eyeLeft.parent.lossyScale.x;
        _eyeLeft.localScale = scale;
        _eyeRight.localScale = scale;
    }
    #endregion
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The first boss's entrance. The spawner places him on the platform from the start of the level as an untextured,
/// frozen statue that can't be hit. When the player drops off the ledge the cutscene takes over: the player rolls down
/// and lands, the statue shakes, its eyes grow, its real texture spreads over it, the camera pulls out to frame the
/// two of them, and a fade hands control back. Holding ESC for a few seconds skips to the end.
///
/// It does nothing on its own: bosses spawned any other way, or in another level, just fight as normal.
/// </summary>
public class BossIntroCutscene : MonoBehaviour
{
    private const float SkipHoldSeconds = 3f;
    private const float BarHeight = 0.12f; // of the screen, top and bottom
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
    private const float FadeOutSeconds = 0.6f;
    private const float FadeInSeconds = 0.7f;
    private const float PaintStartRadius = -2f; // far enough below zero that the brush noise can't show any paint yet
    private const float PaintEndRadius = 11f;   // past his feet and fingertips, measured from his eyes
    private const float LookAngle = 65f;
    private const float OrbitRadius = 5.5f;
    private static readonly Color LandingDust = new Color(0.55f, 0.5f, 0.42f, 1f);

    [SerializeField] private string playsInScene = "Dungeon Level";
    [SerializeField, Tooltip("The scene object holding this room's torches; every light under it goes out and comes back.")]
    private string roomName = "Boss Chamber";

    [Header("Look")]
    [SerializeField] private Material clayMaterial;
    [SerializeField] private Material eyeMaterial;
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("Eyes")]
    [SerializeField, Tooltip("From his head bone, in his own axes: x to each side, y up, z forward.")]
    private Vector3 eyeOffset = new Vector3(0.17f, 0.12f, 0.55f);
    [SerializeField, Tooltip("Full size of each eye: width, height, depth.")]
    private Vector3 eyeShape = new Vector3(0.3f, 0.05f, 0.1f);
    [SerializeField, Tooltip("Degrees each eye tilts, inner corner down, for a glare.")]
    private float eyeSlant = 18f;

    [Header("Player")]
    [SerializeField, Tooltip("The player lands at most this far from him, so that he notices them when the fight starts.")]
    private float maxLandingDistance = 20f;

    private EnemySpawner _spawner;
    private ChamberData _chamber;

    private BaseEnemy _boss;
    private Animator _bossAnimator;
    private NavMeshAgent _bossAgent;
    private Collider _bossCollider;
    private Renderer[] _bossRenderers;
    private Material[][] _paintedMaterials;
    private MaterialPropertyBlock _paintBlock;
    private Vector3 _paintCenter;
    private Transform _eyeLeft, _eyeRight;
    private Vector3 _bossHome;

    private readonly List<Torch> _torches = new List<Torch>();
    private Light _roomLight;
    private float _roomLightIntensity;
    private Color _clayColor;

    private Transform _player;
    private Transform _playerHead;
    private float _headYaw;
    private PlayerMovement _movement;
    private CharacterController _controller;
    private PlayerManager _playerState;
    private Animator _playerAnimator;
    private Vector3 _landing;
    private Vector3 _toBoss;
    private Vector3 _side;

    private Transform _camera;
    private Vector3 _cameraLocalPosition;
    private Quaternion _cameraLocalRotation;
    private Vector3 _shotPosition;
    private Vector3 _shotLook;
    private float _cameraShake;

    private GameObject _overlay;
    private RectTransform _topBar, _bottomBar;
    private Image _fade, _skipRing;
    private Sprite _ringSprite;
    private GameObject _hud;

    private bool _armed, _playing, _skipping;
    private float _skipHeld;

    public bool PlaysInThisScene => SceneManager.GetActiveScene().name == playsInScene;

    /// <summary>Turns the freshly spawned boss into the statue, and starts waiting for the player.</summary>
    public void Arm(EnemySpawner spawner, ChamberData chamber)
    {
        _spawner = spawner;
        _chamber = chamber;
        _armed = true;

        _boss = GetComponent<BaseEnemy>();
        _bossAnimator = GetComponentInChildren<Animator>();
        _bossAgent = GetComponent<NavMeshAgent>();
        _bossCollider = GetComponent<Collider>();

        // Everything comes back on in Wake. Until then he has no AI, no collider to hit and no agent to hold him still.
        _boss.enabled = false;
        _bossAgent.enabled = false;
        if (_bossCollider != null) _bossCollider.enabled = false;

        transform.position = EnemyFx.FloorUnder(transform.position);
        FaceTheRoom();
        _bossAnimator.applyRootMotion = false;
        _bossAnimator.speed = 0f;

        WearClay();
    }

    // The statue looks out over the platform, towards where the player will come in.
    private void FaceTheRoom()
    {
        if (_chamber.chamberTriggers.Count == 0 || _chamber.chamberTriggers[0] == null) return;

        Vector3 toRoom = _chamber.chamberTriggers[0].bounds.center - transform.position;
        toRoom.y = 0f;
        if (toRoom.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toRoom);
    }

    private void WearClay()
    {
        _bossRenderers = GetComponentsInChildren<Renderer>();
        _paintedMaterials = new Material[_bossRenderers.Length][];
        _paintBlock = new MaterialPropertyBlock();
        _clayColor = clayMaterial.GetColor("_ClayColor");

        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            // Instances, so that his dissolve on death still finds them; clear whatever dissolve the asset was saved with.
            Material[] painted = _bossRenderers[i].materials;
            Material[] clay = new Material[painted.Length];
            for (int m = 0; m < painted.Length; m++)
            {
                if (painted[m].HasProperty("_Dissolve")) painted[m].SetFloat("_Dissolve", 0f);

                clay[m] = new Material(clayMaterial);
                clay[m].SetTexture("_BaseMap", painted[m].GetTexture("_BaseMap"));
                clay[m].SetColor("_BaseColor", painted[m].GetColor("_BaseColor"));
            }

            _paintedMaterials[i] = painted;
            _bossRenderers[i].sharedMaterials = clay;
        }

        SetPaintRadius(PaintStartRadius);
    }

    private void Update()
    {
        if (!_armed) return;

        if (_playing)
        {
            UpdateSkip();
            return;
        }

        if (_chamber.isCleared) return; // a loaded save already beat him; the spawner removes him

        if (_player == null && !FindPlayer()) return;

        if (_chamber.hasSpawned || PlayerIsDroppingIn()) Begin();
    }

    private bool FindPlayer()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return false;

        _player = found.transform;
        _movement = found.GetComponent<PlayerMovement>();
        _controller = found.GetComponent<CharacterController>();
        _playerState = found.GetComponent<PlayerManager>();
        _playerAnimator = found.GetComponentInChildren<Animator>();
        return true;
    }

    // Off the ledge and over the platform. The chamber's own trigger only fires right above the floor, too late to
    // show the player coming down.
    private bool PlayerIsDroppingIn()
    {
        if (_controller.isGrounded || _chamber.chamberTriggers.Count == 0 || _chamber.chamberTriggers[0] == null) return false;

        Bounds plate = _chamber.chamberTriggers[0].bounds;
        Vector3 p = _player.position;
        return p.x > plate.min.x && p.x < plate.max.x && p.z > plate.min.z && p.z < plate.max.z
            && p.y > plate.max.y && p.y < plate.max.y + 12f;
    }

    private void Begin()
    {
        _playing = true;
        _spawner.OnChamberTriggerEntered(_chamber);

        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);

        // The player script also drives the camera, so switching it off leaves the camera to us. Staggered makes
        // combat drop whatever was buffered or charging.
        _movement.enabled = false;
        _controller.enabled = false;
        _playerState.SetPlayerState(PlayerState.Staggered);
        _playerAnimator.speed = 1f;

        _camera = Camera.main.transform;
        _cameraLocalPosition = _camera.localPosition;
        _cameraLocalRotation = _camera.localRotation;
        if (PlayerCamera.Instance != null) PlayerCamera.Instance.ClearLockOnTarget();

        if (DeathScreenShatter.Instance != null) _hud = DeathScreenShatter.Instance.mainHUD;
        if (_hud != null) _hud.SetActive(false);

        _playerHead = _playerAnimator.GetBoneTransform(HumanBodyBones.Head);

        _bossHome = transform.position;
        WorkOutLanding();
        FaceBoss();
        FindTorches();
        BuildOverlay();

        StartCoroutine(Timeline());
    }

    private void WorkOutLanding()
    {
        Vector3 away = _player.position - _bossHome;
        away.y = 0f;
        float distance = away.magnitude;
        away /= Mathf.Max(0.01f, distance);

        _toBoss = -away;
        _side = Vector3.Cross(Vector3.up, away);

        // A little further in than the ledge, so the fall reads as a roll forward rather than a drop on the spot.
        Vector3 spot = _bossHome + away * Mathf.Min(maxLandingDistance, distance - 4f);
        spot.y = _bossHome.y + 1f; // his floor: the player may still be too high for the floor check to reach it

        float feetBelowRoot = _controller.height * 0.5f - _controller.center.y;
        _landing = EnemyFx.FloorUnder(spot) + Vector3.up * feetBelowRoot;
    }

    private void FaceBoss()
    {
        _player.rotation = Quaternion.LookRotation(_toBoss);
        transform.rotation = Quaternion.LookRotation(-_toBoss);
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
    private IEnumerator Timeline()
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

    // A camera move from one framing to another; the two are the same for a held or tracking shot.
    private IEnumerator Shot(Vector3 fromPosition, Vector3 toPosition, Func<Vector3> fromLook, Func<Vector3> toLook, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            _shotPosition = Vector3.Lerp(fromPosition, toPosition, t);
            _shotLook = Vector3.Lerp(fromLook(), toLook(), t);
            yield return null;
        }

        _shotPosition = toPosition;
        _shotLook = toLook();
    }

    private void LateUpdate()
    {
        // Here because the animator has only just posed the head for this frame.
        if (_playing && _headYaw != 0f && _playerHead != null)
        {
            _playerHead.rotation = Quaternion.AngleAxis(_headYaw, Vector3.up) * _playerHead.rotation;
        }

        if (!_playing || _camera == null || _shotLook == Vector3.zero) return;

        _camera.position = _shotPosition + UnityEngine.Random.insideUnitSphere * _cameraShake;
        _camera.rotation = Quaternion.LookRotation(_shotLook - _camera.position);
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

    private IEnumerator Paint(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            SetPaintRadius(Mathf.Lerp(PaintStartRadius, PaintEndRadius, elapsed / seconds));
            yield return null;
        }

        SetPaintRadius(PaintEndRadius);
    }

    // On the renderers, not the materials: his dissolve script swaps the renderers onto copies of whatever they wear.
    private void SetPaintRadius(float radius)
    {
        _paintBlock.SetVector("_RevealCenter", _paintCenter);
        _paintBlock.SetFloat("_RevealRadius", radius);
        foreach (Renderer bossRenderer in _bossRenderers) bossRenderer.SetPropertyBlock(_paintBlock);
    }

    private void SetClayBrightness(float brightness)
    {
        _paintBlock.SetColor("_ClayColor", _clayColor * brightness);
        foreach (Renderer bossRenderer in _bossRenderers) bossRenderer.SetPropertyBlock(_paintBlock);
    }
    #endregion

    #region Ending
    private void UpdateSkip()
    {
        if (_skipping) return;

        bool held = (Keyboard.current != null && Keyboard.current.escapeKey.isPressed) ||
                    (Gamepad.current != null && Gamepad.current.startButton.isPressed);

        _skipHeld = held ? _skipHeld + Time.unscaledDeltaTime : Mathf.Max(0f, _skipHeld - Time.unscaledDeltaTime * 2f);
        _skipRing.fillAmount = _skipHeld / SkipHoldSeconds;

        if (_skipHeld >= SkipHoldSeconds)
        {
            _skipping = true;
            StopAllCoroutines();
            StartCoroutine(Skip());
        }
    }

    private IEnumerator Skip()
    {
        yield return Fade(_fade.color.a, 1f, 0.3f);
        yield return HandBack();
    }

    // Starts on a black screen. Puts everything where the end of the cutscene would have left it, then fades in.
    private IEnumerator HandBack()
    {
        AllLightsOn();
        _headYaw = 0f;

        transform.position = _bossHome;
        transform.rotation = Quaternion.LookRotation(-_toBoss);
        _bossAnimator.speed = 1f;

        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            _bossRenderers[i].SetPropertyBlock(null);
            _bossRenderers[i].sharedMaterials = _paintedMaterials[i];
        }

        if (_eyeLeft != null) Destroy(_eyeLeft.gameObject);
        if (_eyeRight != null) Destroy(_eyeRight.gameObject);

        _player.position = _landing;
        _player.rotation = Quaternion.LookRotation(_toBoss);
        _playerAnimator.speed = 1f;
        _playerAnimator.CrossFadeInFixedTime("Locomotion", 0f);
        _controller.enabled = true;
        _movement.ResetMotion();
        _playerState.SetPlayerState(PlayerState.Idle);
        _movement.enabled = true;

        _shotLook = Vector3.zero; // the camera is the player's again
        _cameraShake = 0f;
        _camera.localPosition = _cameraLocalPosition;
        _camera.localRotation = _cameraLocalRotation;
        if (PlayerCamera.Instance != null) PlayerCamera.Instance.FocusOn(_player);

        if (_hud != null) _hud.SetActive(true);
        if (_skipRing != null) _skipRing.transform.parent.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(0.15f); // lets the camera settle behind the player under the black

        StartCoroutine(Bars(1f, 0f, FadeInSeconds));
        yield return Fade(1f, 0f, FadeInSeconds);

        Wake();
    }

    private void Wake()
    {
        _armed = false;
        _playing = false;

        _bossAgent.enabled = true;
        if (_bossCollider != null) _bossCollider.enabled = true;
        _boss.enabled = true;

        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);

        Destroy(_overlay);
        Destroy(_ringSprite.texture);
        Destroy(_ringSprite);
        Destroy(this);
    }
    #endregion

    #region Overlay
    private void BuildOverlay()
    {
        _overlay = new GameObject("BossIntroOverlay");
        Canvas canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        CanvasScaler scaler = _overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _fade = CreateImage("Fade", _overlay.transform, Color.black);
        Stretch(_fade.rectTransform, Vector2.zero, Vector2.one);
        SetFade(0f);

        _topBar = CreateImage("TopBar", _overlay.transform, Color.black).rectTransform;
        _bottomBar = CreateImage("BottomBar", _overlay.transform, Color.black).rectTransform;
        SetBars(0f);

        BuildSkipMeter();
    }

    private void BuildSkipMeter()
    {
        _ringSprite = CreateRingSprite();

        // In the corner of the bottom bar, where it covers none of the picture.
        RectTransform meter = new GameObject("SkipMeter", typeof(RectTransform)).GetComponent<RectTransform>();
        meter.SetParent(_overlay.transform, false);
        meter.anchorMin = meter.anchorMax = new Vector2(1f, 0f);
        meter.pivot = new Vector2(1f, 0f);
        meter.anchoredPosition = new Vector2(-40f, 24f);
        meter.sizeDelta = new Vector2(84f, 84f);

        Image track = CreateImage("Track", meter, new Color(1f, 1f, 1f, 0.2f));
        track.sprite = _ringSprite;
        Stretch(track.rectTransform, Vector2.zero, Vector2.one);

        _skipRing = CreateImage("Fill", meter, Color.white);
        _skipRing.sprite = _ringSprite;
        _skipRing.type = Image.Type.Filled;
        _skipRing.fillMethod = Image.FillMethod.Radial360;
        _skipRing.fillOrigin = (int)Image.Origin360.Top;
        _skipRing.fillClockwise = true;
        _skipRing.fillAmount = 0f;
        Stretch(_skipRing.rectTransform, Vector2.zero, Vector2.one);

        TMP_Text key = CreateLabel("Key", meter, "ESC", 22f, TextAlignmentOptions.Center);
        Stretch(key.rectTransform, Vector2.zero, Vector2.one);

        string hint = new LocalizedString("UIStrings", "HoldToSkip").GetLocalizedString();
        TMP_Text label = CreateLabel("Hint", meter, hint, 26f, TextAlignmentOptions.MidlineRight);
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(1f, 0.5f);
        label.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
        label.rectTransform.sizeDelta = new Vector2(500f, 0f);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text CreateLabel(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        if (labelFont != null) label.font = labelFont;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        const float outer = 0.5f, inner = 0.4f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) / size;
                float alpha = Mathf.Clamp01((outer - radius) * size) * Mathf.Clamp01((radius - inner) * size);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f);
    }

    private void SetFade(float alpha)
    {
        Color color = _fade.color;
        color.a = alpha;
        _fade.color = color;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(from, to, elapsed / seconds));
            yield return null;
        }

        SetFade(to);
    }

    private void SetBars(float amount)
    {
        float height = BarHeight * amount;
        Stretch(_topBar, new Vector2(0f, 1f - height), Vector2.one);
        Stretch(_bottomBar, Vector2.zero, new Vector2(1f, height));
    }

    private IEnumerator Bars(float from, float to, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            SetBars(Mathf.SmoothStep(from, to, elapsed / seconds));
            yield return null;
        }

        SetBars(to);
    }
    #endregion
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// What every boss entrance shares. The spawner places the boss on the platform from the start of the level as an
/// untextured, frozen statue that can't be hit. When the player drops off the ledge the cutscene takes over the player
/// and the camera, each boss plays its own <see cref="Timeline"/>, and a fade hands control back. Holding ESC for a few
/// seconds skips to the end.
///
/// It does nothing on its own: bosses spawned any other way, or in another level, just fight as normal.
/// </summary>
public abstract class BossIntroCutscene : MonoBehaviour
{
    private const float SkipHoldSeconds = 2f;
    private const float BarHeight = 0.12f; // of the screen, top and bottom
    private const float FadeInSeconds = 0.7f;
    protected const float FadeOutSeconds = 0.6f;
    protected const float PaintStartRadius = -2f; // far enough below zero that the brush noise can't show any paint yet
    private const float PaintEndRadius = 11f;     // past the feet and fingertips, measured from where the paint starts

    [SerializeField] private string playsInScene;

    [Header("Look")]
    [SerializeField] private Material clayMaterial;
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("Player")]
    [SerializeField, Tooltip("The player lands at most this far from the boss, so that the boss notices them when the fight starts.")]
    private float maxLandingDistance = 20f;

    private EnemySpawner _spawner;
    private ChamberData _chamber;

    private BaseEnemy _boss;
    protected Animator _bossAnimator;
    private NavMeshAgent _bossAgent;
    private Collider _bossCollider;
    private Renderer[] _bossRenderers;
    private Material[][] _paintedMaterials;
    private MaterialPropertyBlock _paintBlock;
    private Color _clayColor;
    protected Vector3 _paintCenter;
    protected Vector3 _bossHome;

    protected Transform _player;
    private PlayerMovement _movement;
    private CharacterController _controller;
    private PlayerManager _playerState;
    protected Animator _playerAnimator;
    protected Vector3 _landing;
    protected Vector3 _toBoss;
    protected Vector3 _side;

    private Transform _camera;
    private Vector3 _cameraLocalPosition;
    private Quaternion _cameraLocalRotation;
    protected Vector3 _shotPosition;
    protected Vector3 _shotLook;
    protected float _cameraShake;

    private GameObject _overlay;
    private RectTransform _topBar, _bottomBar;
    private Image _fade, _skipRing;
    private Sprite _ringSprite;
    private GameObject _hud;

    private bool _armed, _skipping;
    protected bool _playing;
    private float _skipHeld;

    public bool PlaysInThisScene => SceneManager.GetActiveScene().name == playsInScene;

    /// <summary>The boss's own part of the cutscene. It ends by fading to black and yielding <see cref="HandBack"/>.</summary>
    protected abstract IEnumerator Timeline();

    /// <summary>Called as the cutscene starts, once the player and the landing spot are known.</summary>
    protected virtual void OnBegin() { }

    /// <summary>Called on a black screen, at the normal end or after a skip: undo anything the timeline left behind.</summary>
    protected virtual void RestoreScene() { }

    /// <summary>Called once the boss has become the waiting statue, as the level starts.</summary>
    protected virtual void OnArmed() { }

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

        // Everything comes back on in Wake. Until then it has no AI, no collider to hit and no agent to hold it still.
        _boss.enabled = false;
        _bossAgent.enabled = false;
        if (_bossCollider != null) _bossCollider.enabled = false;

        transform.position = EnemyFx.FloorUnder(transform.position);
        FaceTheRoom();
        _bossAnimator.applyRootMotion = false;
        _bossAnimator.speed = 0f;

        WearClay();
        OnArmed();
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
            // Instances, so that the dissolve on death still finds them; clear whatever dissolve the asset was saved with.
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

        // While it waits it's scenery, not an enemy to find: the last-enemies hint draws everything on the Enemy layer
        // through walls, which showed the statue's outline from rooms away. Wake puts the layers back.
        _rendererLayers = new int[_bossRenderers.Length];
        int scenery = LayerMask.NameToLayer("Default");
        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            _rendererLayers[i] = _bossRenderers[i].gameObject.layer;
            _bossRenderers[i].gameObject.layer = scenery;
        }
    }

    private int[] _rendererLayers;

    private void Update()
    {
        if (!_armed) return;

        if (_playing)
        {
            UpdateSkip();
            return;
        }

        if (_chamber.isCleared) return; // a loaded save already beat the boss; the spawner removes it

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

        _bossHome = transform.position;
        WorkOutLanding();
        FaceEachOther();
        BuildOverlay();
        OnBegin();

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

        // A little further in than the ledge, so the player ends up clear of it.
        Vector3 spot = _bossHome + away * Mathf.Min(maxLandingDistance, distance - 4f);
        _landing = StandingAt(spot);
    }

    /// <summary>Where the player's root goes to stand on the boss's floor at this point.</summary>
    protected Vector3 StandingAt(Vector3 spot)
    {
        spot.y = _bossHome.y + 1f; // the player may still be too high for the floor check to reach it
        float feetBelowRoot = _controller.height * 0.5f - _controller.center.y;
        return EnemyFx.FloorUnder(spot) + Vector3.up * feetBelowRoot;
    }

    private void FaceEachOther()
    {
        _player.rotation = Quaternion.LookRotation(_toBoss);
        transform.rotation = Quaternion.LookRotation(-_toBoss);
    }

    // A camera move from one framing to another; the two are the same for a held or tracking shot.
    protected IEnumerator Shot(Vector3 fromPosition, Vector3 toPosition, Func<Vector3> fromLook, Func<Vector3> toLook, float seconds)
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

    protected virtual void LateUpdate()
    {
        if (!_playing || _camera == null || _shotLook == Vector3.zero) return;

        _camera.position = _shotPosition + UnityEngine.Random.insideUnitSphere * _cameraShake;
        _camera.rotation = Quaternion.LookRotation(_shotLook - _camera.position);
    }

    protected IEnumerator Paint(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            SetPaintRadius(Mathf.Lerp(PaintStartRadius, PaintEndRadius, elapsed / seconds));
            yield return null;
        }

        SetPaintRadius(PaintEndRadius);
    }

    // On the renderers, not the materials: the dissolve script swaps the renderers onto copies of whatever they wear.
    private void SetPaintRadius(float radius)
    {
        _paintBlock.SetVector("_RevealCenter", _paintCenter);
        _paintBlock.SetFloat("_RevealRadius", radius);
        foreach (Renderer bossRenderer in _bossRenderers) bossRenderer.SetPropertyBlock(_paintBlock);
    }

    protected void SetClayBrightness(float brightness)
    {
        _paintBlock.SetColor("_ClayColor", _clayColor * brightness);
        foreach (Renderer bossRenderer in _bossRenderers) bossRenderer.SetPropertyBlock(_paintBlock);
    }

    /// <summary>How tall the boss stands, from the renderers.</summary>
    protected float BossHeight()
    {
        float top = _bossHome.y;
        foreach (Renderer bossRenderer in _bossRenderers) top = Mathf.Max(top, bossRenderer.bounds.max.y);
        return top - _bossHome.y;
    }

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
        StoryDialogue.ClearLines();
        yield return Fade(_fade.color.a, 1f, 0.3f);
        yield return HandBack();
    }

    // Starts on a black screen. Puts everything where the end of the cutscene would have left it, then fades in.
    protected IEnumerator HandBack()
    {
        RestoreScene();

        transform.position = _bossHome;
        transform.rotation = Quaternion.LookRotation(-_toBoss);
        _bossAnimator.speed = 1f;

        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            _bossRenderers[i].SetPropertyBlock(null);
            _bossRenderers[i].sharedMaterials = _paintedMaterials[i];
        }

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
        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            if (_bossRenderers[i] != null) _bossRenderers[i].gameObject.layer = _rendererLayers[i];
        }

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

    protected void SetFade(float alpha)
    {
        Color color = _fade.color;
        color.a = alpha;
        _fade.color = color;
    }

    protected IEnumerator Fade(float from, float to, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(from, to, elapsed / seconds));
            yield return null;
        }

        SetFade(to);
    }

    protected void SetBars(float amount)
    {
        float height = BarHeight * amount;
        Stretch(_topBar, new Vector2(0f, 1f - height), Vector2.one);
        Stretch(_bottomBar, Vector2.zero, new Vector2(1f, height));
    }

    protected IEnumerator Bars(float from, float to, float seconds)
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

using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The final boss's entrance. There is no boss at the bottom of Hell, only a tall mirror (<see cref="InkMirror"/>).
/// The knight walks up to it and sees himself. He tilts his head and the reflection doesn't; it turns to ink, walks
/// up to the glass and cracks it, and the glass bursts. The knight rolls back from it, and the shadow rises out of
/// the ink in front of the empty frame, many times his size.
/// </summary>
public class InkShadowIntro : BossIntroCutscene
{
    private const float MirrorBehindBoss = 1.6f;  // the boss rises just in front of the glass
    private const float StandBack = 2.6f;         // how far from the glass the knight stops
    private const float WalkSpeedScale = 0.8f;    // of the knight's walk speed
    private const float RollBackDistance = 3.4f;
    private const float RollSeconds = 0.75f;
    private const float RiseSeconds = 1.1f;
    private const float PaintSeconds = 2.8f;
    private const float PaintAboveHead = 0.2f;
    private const float HeadTiltDegrees = 24f;
    private const string IdleState = "HumanM@Idle01";

    [SerializeField] private GameObject mirrorPrefab;

    private InkMirror _mirror;
    private Renderer[] _parts;
    private Vector3 _bossScale;
    private AnimationBridge _playerBridge;
    private Transform _playerHead;
    private float _headTilt;
    private Vector3 _tiltAxis;

    // As the level starts: no boss to see, only the mirror it will come out of.
    protected override void OnArmed()
    {
        _bossScale = transform.localScale;
        _parts = GetComponentsInChildren<Renderer>(true);
        SetBossVisible(false);

        Vector3 at = transform.position - transform.forward * MirrorBehindBoss;
        _mirror = Instantiate(mirrorPrefab, EnemyFx.FloorUnder(at) - Vector3.up * 0.05f, transform.rotation).GetComponent<InkMirror>();
    }

    protected override void OnBegin()
    {
        _playerBridge = _player.GetComponent<AnimationBridge>();
        _playerHead = _playerAnimator.GetBoneTransform(HumanBodyBones.Head);

        // The paint starts at his head and runs down to his feet.
        Transform head = _bossAnimator.GetBoneTransform(HumanBodyBones.Head);
        float headHeight = head != null ? head.position.y - _bossHome.y : BossHeight() * 0.9f;
        _paintCenter = _bossHome + Vector3.up * (headHeight + PaintAboveHead);

        // Its lines are timed to the pictures, so nothing still waiting to be said may hold them up.
        StoryDialogue.ClearLines();

        // The player has only just arrived: black covers the jump to standing on the floor.
        SetFade(1f);
        SetBars(1f);
        _player.position = _landing;
        _player.rotation = Quaternion.LookRotation(_toBoss);
        _playerAnimator.CrossFadeInFixedTime("Locomotion", 0f);
    }

    protected override void RestoreScene()
    {
        if (_mirror != null) Destroy(_mirror.gameObject); // the empty frame goes under the black
        SetBossVisible(true);
        transform.localScale = _bossScale;
        _bossAnimator.Play(IdleState, 0, 0f);

        _headTilt = 0f;
        if (_playerBridge != null) _playerBridge.enabled = true;
    }

    private void SetBossVisible(bool visible)
    {
        foreach (Renderer part in _parts) if (part != null) part.enabled = visible;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    protected override IEnumerator Timeline()
    {
        // He ends up square in front of the glass, so that his reflection stands in the middle of it.
        Vector3 toMirror = -Flat(_mirror.transform.forward).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toMirror);
        Vector3 glass = _mirror.GlassCentre;
        Vector3 stand = StandingAt(_mirror.transform.position - toMirror * StandBack);
        Func<Vector3> reflection = () => _mirror != null ? _mirror.ReflectionFace : glass;

        // A wide shot from the side: the knight, and the mirror standing alone across the floor.
        Vector3 wide = _landing - toMirror * 3f + right * 8f + Vector3.up * 3f;
        Func<Vector3> between = () => Vector3.Lerp(_player.position, glass, 0.55f);
        StartCoroutine(Shot(wide, wide + toMirror * 2f, between, between, 4.2f));
        yield return Fade(1f, 0f, 0.9f);
        StoryDialogue.Say("Story_A3_Boss_01");
        yield return new WaitForSecondsRealtime(3.3f);

        // He walks up to it. Over his shoulder, his reflection walks up to meet him.
        float walkSpeed = _player.GetComponent<PlayerMovement>().walkSpeed * WalkSpeedScale;
        float walkSeconds = Flat(stand - _player.position).magnitude / Mathf.Max(0.5f, walkSpeed);
        StartCoroutine(Walk(stand, walkSeconds));
        StoryDialogue.Say("Story_A3_Boss_02");
        yield return FollowWalk(walkSeconds + 0.3f, toMirror, right, glass);

        // Face to face. He tilts his head; the reflection doesn't.
        Vector3 shoulder = stand - toMirror * 3.4f + right * 1.3f + Vector3.up * 0.8f;
        yield return Shot(_shotPosition, shoulder, () => glass, reflection, 1.1f);
        yield return new WaitForSecondsRealtime(0.8f);
        _mirror.Freeze();
        _tiltAxis = toMirror;
        yield return TiltHead(1.8f);

        // It drains to ink, and speaks.
        StartCoroutine(_mirror.TurnToInk(1.8f));
        StartCoroutine(Shot(shoulder, shoulder + toMirror * 0.6f, reflection, reflection, 2.6f));
        yield return new WaitForSecondsRealtime(1.2f);
        StoryDialogue.Say("Story_A3_Boss_03", Speaker.Shadow);
        yield return new WaitForSecondsRealtime(1.8f);

        // It walks up to the glass from its side, and the glass gives.
        yield return _mirror.StepToGlass(1.1f);
        _cameraShake = 0.02f;
        yield return _mirror.Crack(1.1f);
        _cameraShake = 0f;
        yield return new WaitForSecondsRealtime(0.35f);

        // The glass bursts. The knight rolls back from it, and the shadow rises out of the ink in front of the frame.
        Vector3 middle = Vector3.Lerp(stand, _bossHome, 0.5f);
        Vector3 side = middle + right * 10f - toMirror * 3f + Vector3.up * 3f;
        Func<Vector3> both = () => Vector3.Lerp(_player.position, _bossHome, 0.55f) + Vector3.up * 1.6f;
        _shotPosition = side;
        _shotLook = both();
        StartCoroutine(Shot(side, side + toMirror * 1.5f, both, both, 2.6f));
        _mirror.Shatter();
        _cameraShake = 0.15f;
        StartCoroutine(RollBack(toMirror));
        yield return new WaitForSecondsRealtime(0.25f);
        _cameraShake = 0f;
        StartCoroutine(Rise());
        yield return new WaitForSecondsRealtime(1f);
        StoryDialogue.Say("Story_A3_Boss_04", Speaker.Shadow);
        yield return new WaitForSecondsRealtime(1.4f);

        // The ink settles into its shape from the head down, and runs out before it reaches the feet.
        float height = BossHeight();
        Func<Vector3> chest = () => _bossHome + Vector3.up * height * 0.62f;
        Func<Vector3> face = () => _paintCenter;
        // From in front and well to the side, so that the knight isn't in the way.
        Vector3 low = _bossHome - toMirror * height * 0.8f + right * height * 1.1f + Vector3.up * 1.2f;
        Vector3 lower = low - toMirror * height * 0.5f + right * height * 0.3f;
        StartCoroutine(Paint(PaintSeconds));
        yield return Shot(low, lower, face, chest, PaintSeconds);

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Roar", _bossHome);
        StoryDialogue.Say("Story_A3_Boss_05", Speaker.Shadow, 1.2f);
        Vector3 wideEnd = middle + right * 14f - toMirror * 6f + Vector3.up * 6f;
        Func<Vector3> pair = () => Vector3.Lerp(_player.position, _bossHome, 0.5f) + Vector3.up * 1.8f;
        yield return Shot(lower, wideEnd, chest, pair, 2.6f);

        // Fade out, and fade back in on the player, where the roll left them.
        _landing = _player.position;
        yield return Fade(0f, 1f, FadeOutSeconds);
        yield return HandBack();
    }

    #region The knight
    // The cutscene moves the player itself, so the animation bridge (which reads the switched-off controller as
    // standing still) is off and the walk is fed to the animator by hand.
    private IEnumerator Walk(Vector3 to, float seconds)
    {
        if (_playerBridge != null) _playerBridge.enabled = false;

        Vector3 from = _player.position;
        _player.rotation = Quaternion.LookRotation(Flat(to - from));
        float averageSpeed = Flat(to - from).magnitude / seconds;
        float walkSpeed = _player.GetComponent<PlayerMovement>().walkSpeed;

        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / seconds;
            _player.position = StandingAt(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            float speed = averageSpeed * 6f * t * (1f - t); // how fast the smooth step is moving him
            _playerAnimator.SetFloat("MoveZ", speed / walkSpeed);
            _playerAnimator.SetFloat("Speed", speed);
            _playerAnimator.SetBool("IsGrounded", true);
            _playerAnimator.SetBool("IsFalling", false);
            yield return null;
        }

        _player.position = to;
        _playerAnimator.SetFloat("MoveZ", 0f);
        _playerAnimator.SetFloat("Speed", 0f);
        if (_playerBridge != null) _playerBridge.enabled = true;
    }

    // Behind him and a little to the side, looking past him at the glass.
    private IEnumerator FollowWalk(float seconds, Vector3 toMirror, Vector3 right, Vector3 glass)
    {
        Vector3 startPosition = _shotPosition, startLook = _shotLook;
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            Vector3 behind = _player.position - toMirror * 5.5f + right * 1.7f + Vector3.up * 1.1f;
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / 0.9f);
            _shotPosition = Vector3.Lerp(startPosition, behind, blend);
            _shotLook = Vector3.Lerp(startLook, glass, blend);
            yield return null;
        }
    }

    // Leans his head to one side, holds it, and straightens up.
    private IEnumerator TiltHead(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / seconds;
            _headTilt = HeadTiltDegrees * Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI * 0.5f) * (t < 0.75f ? 1f : Mathf.InverseLerp(1f, 0.75f, t));
            yield return null;
        }
        _headTilt = 0f;
    }

    private IEnumerator RollBack(Vector3 toMirror)
    {
        Vector3 from = _player.position;
        Vector3 to = StandingAt(from - toMirror * RollBackDistance);
        _playerAnimator.CrossFadeInFixedTime("RollBackward", 0.06f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Roll", from);

        for (float elapsed = 0f; elapsed < RollSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = 1f - Mathf.Pow(1f - elapsed / RollSeconds, 2f); // fast off the mark, easing to a stop
            _player.position = StandingAt(Vector3.Lerp(from, to, t));
            yield return null;
        }

        _player.position = to;
        _playerAnimator.CrossFadeInFixedTime("Locomotion", 0.25f);
    }

    protected override void LateUpdate()
    {
        // Here because the animator has only just posed the head for this frame.
        if (_playing && _headTilt != 0f && _playerHead != null)
        {
            _playerHead.rotation = Quaternion.AngleAxis(_headTilt, _tiltAxis) * _playerHead.rotation;
        }

        base.LateUpdate();
    }
    #endregion

    // Up out of a pool of ink: it swells from flat to its full height, with a little overshoot.
    private IEnumerator Rise()
    {
        SetBossVisible(true);
        _bossAnimator.speed = 1f;
        EnemyFx.Impact(_bossHome, 3.5f, InkShadow.InkGlow);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", _bossHome);

        for (float elapsed = 0f; elapsed < RiseSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / RiseSeconds - 1f;
            float grown = 1f + 2.2f * t * t * t + 1.2f * t * t; // ease out, overshooting a touch
            transform.localScale = new Vector3(_bossScale.x, _bossScale.y * Mathf.Max(0.05f, grown), _bossScale.z);
            yield return null;
        }

        transform.localScale = _bossScale;
    }
}

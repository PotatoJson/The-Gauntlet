using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A short cutscene in the middle of a fight (Brutus's enrage): takes the camera and the player's input, draws the
/// black bars, and gives both back at the end. The boss's own coroutine moves the camera with <see cref="Shot"/>.
/// Unlike a boss intro it doesn't move the player or skip; it lasts a few seconds.
/// </summary>
public class CutsceneCamera : MonoBehaviour
{
    private const float BarHeight = 0.12f;

    public Vector3 Position;
    public Vector3 Look;
    public float Shake;

    private Transform _camera;
    private Vector3 _cameraLocalPosition;
    private Quaternion _cameraLocalRotation;
    private PlayerMovement _movement;
    private PlayerManager _playerState;
    private GameObject _hud;
    private RectTransform _top, _bottom;

    public static CutsceneCamera Begin()
    {
        CutsceneCamera cut = new GameObject("CutsceneCamera").AddComponent<CutsceneCamera>();
        cut.Take();
        return cut;
    }

    private void Take()
    {
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);

        // The player script also drives the camera, so switching it off leaves the camera to us.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _movement = player.GetComponent<PlayerMovement>();
            _playerState = player.GetComponent<PlayerManager>();
            if (_movement != null) _movement.enabled = false;
            if (_playerState != null) _playerState.SetPlayerState(PlayerState.Staggered);
        }

        _camera = Camera.main.transform;
        _cameraLocalPosition = _camera.localPosition;
        _cameraLocalRotation = _camera.localRotation;
        Position = _camera.position;
        Look = _camera.position + _camera.forward * 10f;

        if (DeathScreenShatter.Instance != null) _hud = DeathScreenShatter.Instance.mainHUD;
        if (_hud != null) _hud.SetActive(false);
        BuildBars();
        StartCoroutine(Bars(0f, 1f, 0.35f));
    }

    /// <summary>A camera move from one framing to another, in real time.</summary>
    public IEnumerator Shot(Vector3 fromPosition, Vector3 toPosition, Func<Vector3> fromLook, Func<Vector3> toLook, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            Position = Vector3.Lerp(fromPosition, toPosition, t);
            Look = Vector3.Lerp(fromLook(), toLook(), t);
            yield return null;
        }
        Position = toPosition;
        Look = toLook();
    }

    private void LateUpdate()
    {
        if (_camera == null) return;
        _camera.position = Position + UnityEngine.Random.insideUnitSphere * Shake;
        if ((Look - _camera.position).sqrMagnitude > 0.001f) _camera.rotation = Quaternion.LookRotation(Look - _camera.position);
    }

    /// <summary>Gives the camera and the controls back to the player.</summary>
    public void End()
    {
        if (_camera != null)
        {
            _camera.localPosition = _cameraLocalPosition;
            _camera.localRotation = _cameraLocalRotation;
        }
        if (_movement != null)
        {
            _movement.ResetMotion();
            _movement.enabled = true;
            if (PlayerCamera.Instance != null) PlayerCamera.Instance.FocusOn(_movement.transform);
        }
        if (_playerState != null) _playerState.SetPlayerState(PlayerState.Idle);
        if (_hud != null) _hud.SetActive(true);
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
        Destroy(gameObject);
    }

    private void BuildBars()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        _top = Bar("Top", new Vector2(0f, 1f));
        _bottom = Bar("Bottom", new Vector2(0f, 0f));
    }

    private RectTransform Bar(string name, Vector2 pivot)
    {
        Image bar = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        bar.transform.SetParent(transform, false);
        bar.color = Color.black;
        bar.raycastTarget = false;
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = new Vector2(0f, pivot.y);
        rect.anchorMax = new Vector2(1f, pivot.y);
        rect.pivot = pivot;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private IEnumerator Bars(float from, float to, float seconds)
    {
        for (float elapsed = 0f; elapsed <= seconds; elapsed += Time.unscaledDeltaTime)
        {
            float height = Screen.height * BarHeight * Mathf.Lerp(from, to, elapsed / seconds);
            _top.sizeDelta = new Vector2(0f, height);
            _bottom.sizeDelta = new Vector2(0f, height);
            yield return null;
        }
    }
}

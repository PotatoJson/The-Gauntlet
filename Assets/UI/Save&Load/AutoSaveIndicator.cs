using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows an "Auto-saving" label with a spinning ring of dots in the bottom-right corner
/// whenever a checkpoint is written, so the player can tell their progress was banked.
///
/// It builds its own canvas at runtime and bootstraps itself, so there is nothing to place in
/// a scene and nothing to wire in the Inspector. It borrows the font from whatever TMP text is
/// already on screen so it matches the rest of the UI.
/// </summary>
public class AutoSaveIndicator : MonoBehaviour
{
    public static AutoSaveIndicator Instance { get; private set; }

    // --- Look and feel ---
    private const string Message = "Auto-saving";

    private const float FadeInDuration = 0.2f;
    private const float FadeOutDuration = 0.45f;
    private const float HoldDuration = 1.5f;

    private const int DotCount = 8;
    private const float DotRadius = 9f;
    private const float DotSize = 5f;
    private const float SpinStepInterval = 0.09f;

    private const float CornerMargin = 40f;
    private const float FontSize = 22f;

    private CanvasGroup _group;
    private RectTransform _spinner;
    private TextMeshProUGUI _label;

    private Coroutine _showRoutine;
    private bool _fontResolved;
    private bool _isSpinning;
    private float _spinTimer;
    private int _spinStep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject host = new GameObject("[AutoSaveIndicator]");
        host.AddComponent<AutoSaveIndicator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUI();
    }

    private void OnEnable()
    {
        SaveManager.OnCheckpointSaved += Show;
    }

    private void OnDisable()
    {
        SaveManager.OnCheckpointSaved -= Show;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Plays the indicator. Safe to call again while it is already showing.</summary>
    public void Show()
    {
        if (_group == null) return;

        ApplyGameFont();

        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        _isSpinning = true;
        _spinStep = 0;
        _spinTimer = 0f;

        // SetUpdate(true) so the indicator still animates if the game is paused.
        _group.DOKill();
        _group.DOFade(1f, FadeInDuration).SetUpdate(true);

        // Realtime waits, for the same reason.
        yield return new WaitForSecondsRealtime(FadeInDuration + HoldDuration);

        _group.DOKill();
        _group.DOFade(0f, FadeOutDuration).SetUpdate(true);

        yield return new WaitForSecondsRealtime(FadeOutDuration);

        _isSpinning = false;
        _showRoutine = null;
    }

    private void Update()
    {
        if (!_isSpinning || _spinner == null) return;

        // Step the rotation instead of sweeping it, which reads better against pixel art.
        _spinTimer += Time.unscaledDeltaTime;
        if (_spinTimer < SpinStepInterval) return;

        _spinTimer -= SpinStepInterval;
        _spinStep = (_spinStep + 1) % DotCount;
        _spinner.localRotation = Quaternion.Euler(0f, 0f, -_spinStep * (360f / DotCount));
    }

    #region UI Construction

    private void BuildUI()
    {
        // --- Canvas ---
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // Above the HUD, below nothing that matters.

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // No GraphicRaycaster: this must never intercept clicks.

        // --- Group pinned to the bottom-right corner ---
        GameObject groupObject = new GameObject("Group", typeof(RectTransform));
        groupObject.transform.SetParent(transform, false);

        RectTransform groupRect = (RectTransform)groupObject.transform;
        groupRect.anchorMin = new Vector2(1f, 0f);
        groupRect.anchorMax = new Vector2(1f, 0f);
        groupRect.pivot = new Vector2(1f, 0f);
        groupRect.sizeDelta = new Vector2(260f, 44f);
        groupRect.anchoredPosition = new Vector2(-CornerMargin, CornerMargin);

        _group = groupObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        BuildSpinner(groupRect);
        BuildLabel(groupRect);
    }

    private void BuildSpinner(RectTransform parent)
    {
        GameObject spinnerObject = new GameObject("Spinner", typeof(RectTransform));
        spinnerObject.transform.SetParent(parent, false);

        _spinner = (RectTransform)spinnerObject.transform;
        _spinner.anchorMin = new Vector2(1f, 0.5f);
        _spinner.anchorMax = new Vector2(1f, 0.5f);
        _spinner.pivot = new Vector2(0.5f, 0.5f);
        _spinner.sizeDelta = new Vector2(26f, 26f);
        _spinner.anchoredPosition = new Vector2(-13f, 0f);

        Sprite dotSprite = CreateCircleSprite(8);

        // Dots fade out around the ring, so rotating the parent reads as a chasing spinner.
        for (int i = 0; i < DotCount; i++)
        {
            float angle = i * Mathf.PI * 2f / DotCount;

            GameObject dot = new GameObject($"Dot{i}", typeof(RectTransform));
            dot.transform.SetParent(_spinner, false);

            RectTransform dotRect = (RectTransform)dot.transform;
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(DotSize, DotSize);
            dotRect.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * DotRadius;

            Image dotImage = dot.AddComponent<Image>();
            dotImage.sprite = dotSprite;
            dotImage.raycastTarget = false;
            dotImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.15f, i / (float)(DotCount - 1)));
        }
    }

    private void BuildLabel(RectTransform parent)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = new Vector2(1f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 0.5f);
        labelRect.pivot = new Vector2(1f, 0.5f);
        labelRect.sizeDelta = new Vector2(210f, 34f);
        labelRect.anchoredPosition = new Vector2(-34f, 0f);

        _label = labelObject.AddComponent<TextMeshProUGUI>();
        _label.text = Message;
        _label.fontSize = FontSize;
        _label.alignment = TextAlignmentOptions.MidlineRight;
        _label.raycastTarget = false;
        _label.color = Color.white;
    }

    /// <summary>
    /// Adopts the font the rest of the UI is already using so the indicator doesn't stand out.
    ///
    /// This runs on the first Show rather than during construction: the indicator is built
    /// before the first scene loads, when there is no UI on screen to copy from yet. If nothing
    /// is found we keep the TMP default, which is only a cosmetic mismatch.
    /// </summary>
    private void ApplyGameFont()
    {
        if (_fontResolved || _label == null) return;

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (text == _label || text.font == null) continue;

            _label.font = text.font;
            _fontResolved = true;
            return;
        }
    }

    /// <summary>Builds a filled circle sprite in code so no art asset has to be imported.</summary>
    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        float radius = size * 0.5f;
        Vector2 centre = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    #endregion
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The crosshair while a Spirit Bow is out: a dot with four ticks round it. Drawing a shot pulls the ticks in to
/// the dot; at full draw they close on it and light up in the arrow's colour. Over an enemy it turns red.
/// Built in code; <see cref="PlayerBow"/> drives it.
/// </summary>
public class BowReticle : MonoBehaviour
{
    private const float OpenGap = 22f, ClosedGap = 6f;  // pixels from the centre to each tick (1080p)

    private CanvasGroup _group;
    private RectTransform _holder;
    private Image _dot;
    private readonly Image[] _ticks = new Image[4];
    private float _shown;

    public static BowReticle Create()
    {
        var go = new GameObject("Bow Reticle");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000; // over the HUD, under menus
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        var reticle = go.AddComponent<BowReticle>();
        reticle.Build();
        return reticle;
    }

    private void Build()
    {
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _group.alpha = 0f;

        _holder = new GameObject("Centre", typeof(RectTransform)).GetComponent<RectTransform>();
        _holder.SetParent(transform, false);
        _holder.sizeDelta = Vector2.zero;

        _dot = Piece("Dot", new Vector2(6f, 6f));
        for (int i = 0; i < 4; i++)
        {
            bool vertical = i % 2 == 0;
            _ticks[i] = Piece("Tick", vertical ? new Vector2(3f, 12f) : new Vector2(12f, 3f));
        }
    }

    private Image Piece(string name, Vector2 size)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(_holder, false);
        image.rectTransform.sizeDelta = size;
        image.raycastTarget = false;
        // A dark edge so it reads on bright effects and pale skies alike.
        var edge = image.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.7f);
        edge.effectDistance = new Vector2(1.5f, -1.5f);
        return image;
    }

    /// <summary>Where it sits: a point on the screen (in pixels, e.g. the lock-on marker), or null for the middle.</summary>
    public void Place(Vector2? screenPoint)
    {
        if (screenPoint == null) { _holder.anchoredPosition = Vector2.zero; return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPoint.Value, null, out Vector2 local);
        _holder.anchoredPosition = local;
    }

    /// <param name="shown">Whether the bow is up and ready.</param>
    /// <param name="draw">How far a shot is drawn, 0-1.</param>
    /// <param name="colour">The next arrow's element colour.</param>
    /// <param name="onEnemy">Whether it's over an enemy.</param>
    public void Set(bool shown, float draw, Color colour, bool onEnemy)
    {
        _shown = Mathf.MoveTowards(_shown, shown ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        _group.alpha = _shown;
        if (_shown <= 0f) return;

        float gap = Mathf.Lerp(OpenGap, ClosedGap, draw);
        bool full = draw >= 0.99f;
        Color tick = full ? colour * 1.2f : Color.Lerp(Color.white, colour, draw);
        if (onEnemy && !full) tick = Color.Lerp(tick, new Color(1f, 0.3f, 0.25f), 0.7f);
        tick.a = 1f;

        Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        for (int i = 0; i < 4; i++)
        {
            _ticks[i].rectTransform.anchoredPosition = directions[i] * gap;
            _ticks[i].color = tick;
        }
        _dot.color = onEnemy ? new Color(1f, 0.35f, 0.3f) : Color.white;
        _dot.rectTransform.localScale = Vector3.one * (full ? 1.4f : 1f);
    }
}

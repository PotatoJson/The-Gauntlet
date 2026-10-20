using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Scrollbar))]
public class ScrollbarSizeLimiter : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float minSize = 0.1f;
    [SerializeField, Range(0f, 1f)] private float maxSize = 1.0f;

    [Tooltip("For a pixel-art knob: the handle keeps its sprite's proportions whatever the content length, " +
             "so the knob is never squashed or stretched. The size is worked out from the handle's width and the " +
             "Min / Max above are ignored.")]
    [SerializeField] private bool keepHandleAspect;

    private Scrollbar _scrollbar;

    private void Awake()
    {
        _scrollbar = GetComponent<Scrollbar>();

        if (keepHandleAspect && _scrollbar.handleRect != null)
        {
            Image handle = _scrollbar.handleRect.GetComponent<Image>();
            if (handle != null) handle.preserveAspect = true;
        }
    }

    // The ScrollRect writes the scrollbar's size in its own LateUpdate, so a LateUpdate here can lose the race.
    // willRenderCanvases runs after every LateUpdate, which makes this the last word.
    private void OnEnable()
    {
        Canvas.willRenderCanvases += Apply;
        Apply();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= Apply;
    }

    private void Apply()
    {
        if (_scrollbar == null) return;

        float target = keepHandleAspect ? SizeForHandleAspect() : Mathf.Clamp(_scrollbar.size, minSize, maxSize);
        if (!Mathf.Approximately(_scrollbar.size, target)) _scrollbar.size = target;
    }

    // Handle height = size * track length + the handle's own extra height; pick the size that makes
    // width : height match the sprite.
    private float SizeForHandleAspect()
    {
        RectTransform handle = _scrollbar.handleRect;
        if (handle == null) return _scrollbar.size;

        Image image = handle.GetComponent<Image>();
        RectTransform area = handle.parent as RectTransform;
        if (image == null || image.sprite == null || area == null || area.rect.height <= 0f) return _scrollbar.size;

        Rect sprite = image.sprite.rect;
        float wantedHeight = handle.rect.width * sprite.height / sprite.width;
        return Mathf.Clamp((wantedHeight - handle.sizeDelta.y) / area.rect.height, 0.02f, 1f);
    }
}

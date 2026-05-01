using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ScrollRect))]
public class AutoScrollerUI : MonoBehaviour
{
    [SerializeField] private float padding = 15f; // Extra space so the button doesn't hug the edge of the mask

    private ScrollRect _scrollRect;
    private RectTransform _viewport;
    private RectTransform _content;
    private GameObject _lastSelected;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        // If viewport isn't set, fallback to the ScrollRect itself
        _viewport = _scrollRect.viewport != null ? _scrollRect.viewport : _scrollRect.GetComponent<RectTransform>();
        _content = _scrollRect.content;
    }

    private void Update()
    {
        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null || selected == _lastSelected) return;

        // Only scroll if the selected item is actually inside this list
        if (!selected.transform.IsChildOf(_content)) return;

        _lastSelected = selected;
        SnapTo(selected.GetComponent<RectTransform>());
    }

    private void SnapTo(RectTransform target)
    {
        // Get the corners of the target button and the viewport mask
        Vector3[] targetCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);

        Vector3[] viewportCorners = new Vector3[4];
        _viewport.GetWorldCorners(viewportCorners);

        // Convert World Space to Viewport Local Space to accurately compare heights
        for (int i = 0; i < 4; i++)
        {
            targetCorners[i] = _viewport.InverseTransformPoint(targetCorners[i]);
            viewportCorners[i] = _viewport.InverseTransformPoint(viewportCorners[i]);
        }

        // Index 0 is Bottom edge, Index 1 is Top edge
        float targetTop = targetCorners[1].y + padding;
        float targetBottom = targetCorners[0].y - padding;
        float viewTop = viewportCorners[1].y;
        float viewBottom = viewportCorners[0].y;

        float difference = 0f;

        // If the button is above the visible area...
        if (targetTop > viewTop)
        {
            difference = targetTop - viewTop;
        }
        // If the button is below the visible area...
        else if (targetBottom < viewBottom)
        {
            difference = targetBottom - viewBottom;
        }

        // Apply the difference directly to the Content's anchored position
        if (difference != 0f)
        {
            Vector2 newPos = _content.anchoredPosition;
            newPos.y -= difference;
            _content.anchoredPosition = newPos;
        }
    }
}
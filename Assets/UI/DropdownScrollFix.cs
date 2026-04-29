using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ScrollRect))]
public class DropdownScrollFix : MonoBehaviour
{
    private ScrollRect _scrollRect;
    private GameObject _lastSelected;

    void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
    }

    void OnEnable()
    {
        // Reset tracking when the dropdown opens
        _lastSelected = null;
    }

    void Update()
    {
        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (current == null || current == _lastSelected) return;

        // Verify the selected item is part of this dropdown's list
        if (!current.transform.IsChildOf(_scrollRect.content)) return;

        RectTransform selection = current.GetComponent<RectTransform>();
        if (selection != null)
        {
            UpdateScrollPosition(selection);
        }

        _lastSelected = current;
    }

    private void UpdateScrollPosition(RectTransform selection)
    {
        // Calculate where the item is relative to the viewport
        float contentHeight = _scrollRect.content.rect.height;
        float viewportHeight = _scrollRect.viewport.rect.height;

        // Dropdown items are usually anchored to the top
        float selectedItemTop = -selection.localPosition.y - (selection.rect.height / 2f);
        float selectedItemBottom = selectedItemTop + selection.rect.height;

        float currentScrollPos = _scrollRect.verticalNormalizedPosition * (contentHeight - viewportHeight);
        float viewTop = (contentHeight - viewportHeight) - currentScrollPos;
        float viewBottom = viewTop + viewportHeight;

        // If item is out of view, adjust the scrollbar
        if (selectedItemTop < viewTop)
        {
            float newPos = (contentHeight - viewportHeight - selectedItemTop) / (contentHeight - viewportHeight);
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
        }
        else if (selectedItemBottom > viewBottom)
        {
            float newPos = (contentHeight - viewportHeight - (selectedItemBottom - viewportHeight)) / (contentHeight - viewportHeight);
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
        }
    }
}
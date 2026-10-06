using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Put on a parent whose child Buttons (e.g. an arrow icon and its label) should light up together:
/// hovering or selecting any one of them tints all of them. Colors come from the first Button.
/// </summary>
public class LinkedHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Button[] _buttons;
    private bool _hovered;
    private int _lastState = -1;

    private void Awake()
    {
        _buttons = GetComponentsInChildren<Button>(true);
        // We drive the tint ourselves; the buttons' own ColorTint would only react to their own hover.
        foreach (Button b in _buttons) b.transition = Selectable.Transition.None;
    }

    public void OnPointerEnter(PointerEventData e) { _hovered = true; }
    public void OnPointerExit(PointerEventData e) { _hovered = false; }

    private void OnDisable()
    {
        _hovered = false;
        _lastState = -1;
    }

    private void Update()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool isSelected = false;
        foreach (Button b in _buttons) isSelected |= b.gameObject == selected;

        int state = isSelected ? 2 : _hovered ? 1 : 0;
        if (state == _lastState) return;
        _lastState = state;

        ColorBlock c = _buttons[0].colors;
        Color target = state == 2 ? c.selectedColor : state == 1 ? c.highlightedColor : c.normalColor;
        foreach (Button b in _buttons)
        {
            if (b.targetGraphic != null) b.targetGraphic.CrossFadeColor(target, c.fadeDuration, true, true);
        }
    }
}

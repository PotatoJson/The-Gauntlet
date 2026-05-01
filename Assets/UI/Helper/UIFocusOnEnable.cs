using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UIFocusOnEnable : MonoBehaviour
{
    [Tooltip("The button or slider that should be highlighted when this panel opens.")]
    [SerializeField] private GameObject firstItemToSelect;

    [Tooltip("If true, it only forces focus if a controller is connected. Uncheck if you want keyboard to use it too.")]
    [SerializeField] private bool onlyFocusForGamepad = true;

    private void OnEnable()
    {
        if (firstItemToSelect == null || EventSystem.current == null) return;

        // If we only want this to happen for controller players, check if one exists
        if (onlyFocusForGamepad && Gamepad.current == null) return;

        // 1. Clear the current selection to prevent Unity UI bugs
        EventSystem.current.SetSelectedGameObject(null);

        // 2. Snap the focus to the target item
        EventSystem.current.SetSelectedGameObject(firstItemToSelect);
    }
}
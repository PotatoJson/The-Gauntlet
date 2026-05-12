using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections; // NEW: Required for Coroutines

public class UIFocusOnEnable : MonoBehaviour
{
    [Tooltip("The button or slider that should be highlighted when this panel opens.")]
    [SerializeField] private GameObject firstItemToSelect;

    [Tooltip("If true, it only forces focus if a controller is connected. Uncheck if you want keyboard to use it too.")]
    [SerializeField] private bool onlyFocusForGamepad = true;

    private void OnEnable()
    {
        // Start the Coroutine every time the panel turns on
        StartCoroutine(SetFocusNextFrame());
    }

    private IEnumerator SetFocusNextFrame()
    {
        // THE FIX: Wait exactly 1 frame so Unity has time to build the UI panel!
        yield return null;

        if (firstItemToSelect == null || EventSystem.current == null) yield break;

        // If we only want this to happen for controller players, check if one exists
        if (onlyFocusForGamepad && Gamepad.current == null) yield break;

        // 1. Clear the current selection to prevent Unity UI bugs
        EventSystem.current.SetSelectedGameObject(null);

        // 2. Snap the focus to the target item
        EventSystem.current.SetSelectedGameObject(firstItemToSelect);
    }
}
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Outline))] // Ensures the object has an Outline component!
public class UIOutlineToggle : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Outline _outline;

    [Header("Outline Settings")]
    [Tooltip("How thick the outline should be. Larger numbers = thicker outline.")]
    public Vector2 outlineThickness = new Vector2(5f, -5f);

    private void Awake()
    {
        _outline = GetComponent<Outline>();

        if (_outline != null)
        {
            // Set the initial thickness and turn it off by default
            _outline.effectDistance = outlineThickness;
            _outline.enabled = false;
        }
    }

    // --- CONTROLLER FOCUS ---
    public void OnSelect(BaseEventData eventData) => SetOutline(true);
    public void OnDeselect(BaseEventData eventData) => SetOutline(false);

    // --- MOUSE HOVER ---
    public void OnPointerEnter(PointerEventData eventData) => SetOutline(true);
    public void OnPointerExit(PointerEventData eventData) => SetOutline(false);

    // Safety cleanup: turn off if the menu suddenly closes
    private void OnDisable() => SetOutline(false);

    private void SetOutline(bool isOn)
    {
        if (_outline != null)
        {
            _outline.enabled = isOn;

            // Re-apply thickness just in case you tweaked it in the inspector while playing!
            _outline.effectDistance = outlineThickness;
        }
    }
}
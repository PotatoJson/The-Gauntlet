using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIFocusLabel : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TextMeshProUGUI labelText; // Assign the "Resolution" or "FPS" label text here
    [SerializeField] private Color selectedColor = Color.red;
    [SerializeField] private Color normalColor = Color.black;

    public void OnSelect(BaseEventData eventData)
    {
        if (labelText != null) labelText.color = selectedColor;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (labelText != null) labelText.color = normalColor;
    }
    private void OnDisable()
    {
        ResetColor();
    }

    private void ResetColor()
    {
        if (labelText != null) labelText.color = normalColor;
    }
}
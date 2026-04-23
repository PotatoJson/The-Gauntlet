using DG.Tweening;
using UnityEngine;

public class StaminaBar : MonoBehaviour
{
    [SerializeField] private RectTransform barRect;
    private float _maxWidth;

    void Awake()
    {
        // Capture the full width of the bar at the start
        _maxWidth = barRect.sizeDelta.x;
    }

    public void SetStamina(int currentStamina, int maxStamina)
    {
        float staminaPercent = (float)currentStamina / maxStamina;
        float targetWidth = _maxWidth * staminaPercent;

        // Smoothly animate the width over 0.2 seconds
        barRect.DOSizeDelta(new Vector2(targetWidth, barRect.sizeDelta.y), 0.2f).SetUpdate(true);
    }
}
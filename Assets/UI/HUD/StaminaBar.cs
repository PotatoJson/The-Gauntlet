using DG.Tweening;
using UnityEngine;
using UnityEngine.UI; // Required for the Image component

public class StaminaBar : MonoBehaviour
{
    [SerializeField] private Image staminaImage; // Assign the bar image here

    public void SetStamina(int currentStamina, int maxStamina)
    {
        float staminaPercent = (float)currentStamina / maxStamina;

        // Smoothly animate the fill over 0.2 seconds
        if (staminaImage != null)
        {
            staminaImage.DOFillAmount(staminaPercent, 0.2f).SetUpdate(true);
        }
    }
}
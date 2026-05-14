using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class StaminaBar : MonoBehaviour
{
    [SerializeField] private Image staminaImage;

    public void SetStamina(int currentStamina, int maxStamina)
    {
        float staminaPercent = (float)currentStamina / maxStamina;

        if (staminaImage != null)
        {
            // 1. Kill any existing animation on this bar so they don't fight each other
            staminaImage.DOKill();

            // 2. THE FIX: Explicitly tell DOTween how to get and set the fill amount.
            // This guarantees the compiler won't delete the UI code during the build!
            DOTween.To(
                () => staminaImage.fillAmount,     // The Getter
                x => staminaImage.fillAmount = x,  // The Setter
                staminaPercent,                    // The Target Value
                0.2f                               // The Duration
            ).SetUpdate(true).SetTarget(staminaImage);
        }
    }
}
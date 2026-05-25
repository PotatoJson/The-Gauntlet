using UnityEngine;
using UnityEngine.UI;
using TMPro; // --- NEW: Required for TextMeshPro! ---

public class SkillCooldownUI : MonoBehaviour
{
    [Header("Backend References")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("UI Overlays")]
    [Tooltip("Drag the dark overlay images here")]
    [SerializeField] private Image leftSkillOverlay;
    [SerializeField] private Image rightSkillOverlay;

    [Header("UI Countdown Text")]
    [Tooltip("Drag the TextMeshPro text components here")]
    [SerializeField] private TMP_Text leftSkillText;
    [SerializeField] private TMP_Text rightSkillText;

    private void Update()
    {
        if (playerCombat == null) return;

        // --- Update the Left Skill UI (Secondary) ---
        if (leftSkillOverlay != null && playerCombat.LeftMaxCooldown > 0)
        {
            float leftTimer = playerCombat.LeftSkillTimer;

            // Update the dark radial sweep
            leftSkillOverlay.fillAmount = leftTimer / playerCombat.LeftMaxCooldown;

            // Update the Text Countdown
            if (leftSkillText != null)
            {
                if (leftTimer > 0)
                {
                    // CeilToInt rounds 2.1 up to 3, giving a clean 3..2..1 countdown!
                    leftSkillText.text = Mathf.CeilToInt(leftTimer).ToString();
                }
                else
                {
                    leftSkillText.text = ""; // Clear the text when the skill is ready
                }
            }
        }

        // --- Update the Right Skill UI (Primary) ---
        if (rightSkillOverlay != null && playerCombat.RightMaxCooldown > 0)
        {
            float rightTimer = playerCombat.RightSkillTimer;

            // Update the dark radial sweep
            rightSkillOverlay.fillAmount = rightTimer / playerCombat.RightMaxCooldown;

            // Update the Text Countdown
            if (rightSkillText != null)
            {
                if (rightTimer > 0)
                {
                    rightSkillText.text = Mathf.CeilToInt(rightTimer).ToString();
                }
                else
                {
                    rightSkillText.text = ""; // Clear the text when the skill is ready
                }
            }
        }
    }
}
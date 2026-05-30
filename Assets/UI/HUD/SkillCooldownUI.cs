using UnityEngine;
using UnityEngine.UI;
using TMPro; // --- NEW: Required for TextMeshPro! ---

public class SkillCooldownUI : MonoBehaviour
{
    [Header("Backend References")]
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerStatsManager statsManager; // --- NEW: Added for gem check ---

    [Header("UI Containers")] // --- NEW: Added to hide entire skill block ---
    [SerializeField] private GameObject leftSkillContainer;
    [SerializeField] private GameObject rightSkillContainer;

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
        if (playerCombat == null || statsManager == null) return;

        // --- Left Skill (Secondary Gauntlet) ---
        bool hasLeftSkill = statsManager.SecondaryGauntlet != null && statsManager.SecondaryGauntlet.ActiveSkillGem != null;
        if (leftSkillContainer != null) leftSkillContainer.SetActive(hasLeftSkill);

        if (hasLeftSkill && leftSkillOverlay != null && playerCombat.LeftMaxCooldown > 0)
        {
            float leftTimer = playerCombat.LeftSkillTimer;
            leftSkillOverlay.fillAmount = leftTimer / playerCombat.LeftMaxCooldown;

            if (leftSkillText != null)
            {
                leftSkillText.text = leftTimer > 0 ? Mathf.CeilToInt(leftTimer).ToString() : "";
            }
        }

        // --- Right Skill (Primary Gauntlet) ---
        bool hasRightSkill = statsManager.PrimaryGauntlet != null && statsManager.PrimaryGauntlet.ActiveSkillGem != null;
        if (rightSkillContainer != null) rightSkillContainer.SetActive(hasRightSkill);

        if (hasRightSkill && rightSkillOverlay != null && playerCombat.RightMaxCooldown > 0)
        {
            float rightTimer = playerCombat.RightSkillTimer;
            rightSkillOverlay.fillAmount = rightTimer / playerCombat.RightMaxCooldown;

            if (rightSkillText != null)
            {
                rightSkillText.text = rightTimer > 0 ? Mathf.CeilToInt(rightTimer).ToString() : "";
            }
        }
    }
}
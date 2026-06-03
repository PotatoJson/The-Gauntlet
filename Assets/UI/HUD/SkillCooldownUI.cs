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

        // --- Left UI: Primary Gauntlet ---
        bool hasPrimarySkill = statsManager.PrimaryGauntlet != null && statsManager.PrimaryGauntlet.ActiveSkillGem != null;
        if (leftSkillContainer != null) leftSkillContainer.SetActive(hasPrimarySkill);

        if (hasPrimarySkill && leftSkillOverlay != null && playerCombat.RightMaxCooldown > 0)
        {
            float primaryTimer = playerCombat.RightSkillTimer;
            leftSkillOverlay.fillAmount = primaryTimer / playerCombat.RightMaxCooldown;

            if (leftSkillText != null)
            {
                leftSkillText.text = primaryTimer > 0 ? Mathf.CeilToInt(primaryTimer).ToString() : "";
            }
        }

        // --- Right UI: Secondary Gauntlet ---
        bool hasSecondarySkill = statsManager.SecondaryGauntlet != null && statsManager.SecondaryGauntlet.ActiveSkillGem != null;
        if (rightSkillContainer != null) rightSkillContainer.SetActive(hasSecondarySkill);

        if (hasSecondarySkill && rightSkillOverlay != null && playerCombat.LeftMaxCooldown > 0)
        {
            float secondaryTimer = playerCombat.LeftSkillTimer;
            rightSkillOverlay.fillAmount = secondaryTimer / playerCombat.LeftMaxCooldown;

            if (rightSkillText != null)
            {
                rightSkillText.text = secondaryTimer > 0 ? Mathf.CeilToInt(secondaryTimer).ToString() : "";
            }
        }
    }
}
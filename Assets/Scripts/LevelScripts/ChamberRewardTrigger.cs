using UnityEngine;

public enum RewardDropType{
    RandomChance,
    GuaranteedSkillGem,
    GuaranteedStatGem
}

[RequireComponent(typeof(Collider))]
public class ChamberRewardTrigger : MonoBehaviour
{
    [Header("Reward Configuration")]
    [Tooltip("Choose what kind of reward this specific path gives.")]
    public RewardDropType rewardType = RewardDropType.RandomChance;
    
    [Tooltip("If set to RandomChance, what is the % chance of a Skill Gem? (0.1 = 10%)")]
    [Range(0f, 1f)] public float skillGemChance = 0.15f;

    [Header("Skill Gem Settings")]
    public SkillGemData skillGemReward;
    [Tooltip("Drag your pure visual cube prefab here (NO colliders/scripts needed)")]
    public GameObject skillGemVisualPrefab;
    [Tooltip("Where should the physical gem drop?")]
    public Transform gemSpawnLocation;

    [Header("Visuals")]
    [Tooltip("The glowing light/mesh that shows the player a reward is here.")]
    public GameObject glowingVisual;
    public GameObject collectVFX;

    private bool _hasBeenTriggered = false;
    private bool _isSkillGem = false; // Remembers what we rolled
    private GameObject _spawnedGemVisual; // Keeps track of the visual cube
    public GameObject linkedAlternativeReward;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnEnable()
    {
        DetermineAndSpawnReward();
    }

    private void DetermineAndSpawnReward()
    {
        if (glowingVisual != null) glowingVisual.SetActive(true);

        if (rewardType == RewardDropType.GuaranteedSkillGem) _isSkillGem = true;
        else if (rewardType == RewardDropType.GuaranteedStatGem) _isSkillGem = false;
        else
        {
            float roll = Random.value;
            _isSkillGem = (roll <= skillGemChance);
        }

        if (_isSkillGem)
        {
            Debug.Log("[Reward Trigger] Spawning a visual Skill Gem!");
            if (skillGemVisualPrefab != null)
            {
                Vector3 spawnPos = gemSpawnLocation != null ? gemSpawnLocation.position : transform.position;
                _spawnedGemVisual = Instantiate(skillGemVisualPrefab, spawnPos, Quaternion.identity);
                _spawnedGemVisual.transform.SetParent(transform); // Keep the hierarchy clean
            }
        }
        else
        {
            Debug.Log("[Reward Trigger] Stat Gem selected! Waiting for player to touch it...");
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (_hasBeenTriggered) return;

        if (other.CompareTag("Player"))
        {
            _hasBeenTriggered = true;

            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null && playerHealth.CurrentPotions < playerHealth.MaxPotions)
            {
                playerHealth.CurrentPotions++;
                Debug.Log($"[Reward Trigger] Potion refilled. Total: {playerHealth.CurrentPotions}");
            }

            if (glowingVisual != null) glowingVisual.SetActive(false);
            if (collectVFX != null) Instantiate(collectVFX, transform.position, Quaternion.identity);

            if (_isSkillGem)
            {
                PlayerStatsManager stats = other.GetComponent<PlayerStatsManager>();
                GiveSkillGem(stats);
                
                if (_spawnedGemVisual != null) Destroy(_spawnedGemVisual);
            }
            else
            {
                if (RewardMenuManager.Instance != null)
                {
                    RewardMenuManager.Instance.OpenRewardMenu();
                }
                else
                {
                    Debug.LogWarning("RewardMenuManager not found in the scene!");
                }
            }

            if (linkedAlternativeReward != null)
            {
                Debug.Log("[Reward Trigger] Destroying alternative reward path.");
                linkedAlternativeReward.SetActive(false);
            }
            
            gameObject.SetActive(false);
        }
    }

    private void GiveSkillGem(PlayerStatsManager stats)
    {
        if (stats == null || skillGemReward == null) return;

        bool gemAssigned = false;
        
        // Try Primary Gauntlet first
        if (stats.PrimaryGauntlet != null && stats.PrimaryGauntlet.ActiveSkillGem == null)
        {
            stats.PrimaryGauntlet.ActiveSkillGem = skillGemReward;
            gemAssigned = true;
            Debug.Log($"[Reward] {skillGemReward.name} slotted into Primary Gauntlet.");
        }
        // Try Secondary Gauntlet next
        else if (stats.SecondaryGauntlet != null && stats.SecondaryGauntlet.ActiveSkillGem == null)
        {
            stats.SecondaryGauntlet.ActiveSkillGem = skillGemReward;
            gemAssigned = true;
            Debug.Log($"[Reward] {skillGemReward.name} slotted into Secondary Gauntlet.");
        }
        // Fallback: Overwrite Primary if both are full
        else if (stats.PrimaryGauntlet != null)
        {
            stats.PrimaryGauntlet.ActiveSkillGem = skillGemReward;
            gemAssigned = true;
            Debug.Log($"[Reward] {skillGemReward.name} replaced Primary Gauntlet skill.");
        }

        if (gemAssigned) stats.RecalculateGlobalStats();
    }
}
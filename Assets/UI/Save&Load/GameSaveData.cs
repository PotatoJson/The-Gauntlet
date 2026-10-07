using System.Collections.Generic;

/// <summary>
/// The complete snapshot of a run, written to disk as JSON by <see cref="SaveSystem"/>.
/// Everything here has to be a plain public field: JsonUtility ignores properties.
/// </summary>
[System.Serializable]
public class GameSaveData
{
    // Bump this if the layout ever changes so old saves can be rejected instead of half-loaded.
    public const int CurrentVersion = 1;

    public int saveVersion = CurrentVersion;
    public string savedAtUtc;

    // --- Progress ---
    public string sceneName;

    /// <summary>Every level the player has set foot in this run, in the order they were entered.</summary>
    public List<string> visitedLevels = new List<string>();

    /// <summary>Names of the chambers already cleared in <see cref="sceneName"/>.</summary>
    public List<string> clearedChambers = new List<string>();

    /// <summary>
    /// Cleared chambers whose reward was still sitting on the floor when we saved, so
    /// the player doesn't lose a reward by quitting between the clear and the pickup.
    /// </summary>
    public List<string> chambersWithUnclaimedReward = new List<string>();

    /// <summary>The MetricsTracker run ID, so telemetry stays stitched to one run across a quit.</summary>
    public string runID;

    // --- Progression ---
    public int playerLevel = 1;
    public float playerExp;
    public float expToNextLevel = 100f;

    // --- Stats ---
    public float currentHealth;
    public float maxHealthAtSave;

    public float currentStamina;
    public float currentMana = -1f; // -1 = not in the save (older file), start full
    public float currentPoise;
    public int currentPotions;

    // --- Position ---
    public float[] playerPosition = new float[3];
    public float playerYaw;

    // --- Equipment (prefabs are stored by prefab name and resolved through PersistentEquipment) ---
    public string primaryGauntletName;
    public int primaryRarity;
    public List<string> primaryGemNames = new List<string>();
    public List<int> primaryGemTiers = new List<int>();
    public string primarySkillGemName;
    public int primarySkillGemTier = 1;

    public string secondaryGauntletName;
    public int secondaryRarity;
    public List<string> secondaryGemNames = new List<string>();
    public List<int> secondaryGemTiers = new List<int>();
    public string secondarySkillGemName;
    public int secondarySkillGemTier = 1;

    /// <summary>Shrines already used this run, so they stay spent after loading.</summary>
    public List<string> usedShrines = new List<string>();

    public bool IsUsable => saveVersion == CurrentVersion && !string.IsNullOrEmpty(sceneName);

    public UnityEngine.Vector3 GetPosition()
    {
        if (playerPosition == null || playerPosition.Length < 3) return UnityEngine.Vector3.zero;
        return new UnityEngine.Vector3(playerPosition[0], playerPosition[1], playerPosition[2]);
    }

    public void SetPosition(UnityEngine.Vector3 position)
    {
        playerPosition = new[] { position.x, position.y, position.z };
    }

    /// <summary>Short human readable summary for a "Continue" button or save slot.</summary>
    public string GetSummary()
    {
        string level = string.IsNullOrEmpty(sceneName) ? "Unknown" : sceneName;
        int cleared = clearedChambers != null ? clearedChambers.Count : 0;
        return $"{level} - {cleared} chamber(s) cleared - {UnityEngine.Mathf.RoundToInt(currentHealth)} HP";
    }
}

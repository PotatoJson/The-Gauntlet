using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public class PersistentEquipment : MonoBehaviour
{
    public static PersistentEquipment Instance;

    public bool hasSavedData = false;

    [Header("Master Gem Database")]
    [Tooltip("Drag EVERY gem prefab in your game here so the Backpack can always find them!")]
    public List<GameObject> masterGemDatabase = new List<GameObject>();

    [Header("Master Gauntlet Database")]
    [Tooltip("Drag EVERY gauntlet prefab in your game here so a save file can be turned back into real gauntlets. " +
             "Right-click this component's header and pick 'Auto-Populate Prefab Databases' to fill both lists automatically.")]
    public List<GameObject> masterGauntletDatabase = new List<GameObject>();

    [Header("Saved Primary")]
    public GameObject primaryGauntletPrefab;
    public GauntletRarity primaryRarity;
    public List<GameObject> primaryGems = new List<GameObject>();
    [FormerlySerializedAs("ultimateGemPrefab")]
    public GameObject primarySkillGemPrefab;

    [Header("Saved Secondary")]
    public GameObject secondaryGauntletPrefab;
    public GauntletRarity secondaryRarity;
    public List<GameObject> secondaryGems = new List<GameObject>();
    public GameObject secondarySkillGemPrefab;

    [Header("Saved Progression")]
    [Tooltip("The player's level and EXP, carried between levels alongside the gear. Filled in " +
             "automatically from the EXP bar - these values are a live readout, not settings.")]
    public int playerLevel = 1;
    public float playerExp = 0f;
    public float expToNextLevel = 100f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ForceLoadOnSpawn();
        }
    }

    public void SaveEquipment(InventoryManager inv)
    {
        hasSavedData = true;

        primaryGauntletPrefab = inv.activePrimaryPrefab;
        secondaryGauntletPrefab = inv.activeSecondaryPrefab;

        // Pass 'true' everywhere below: the character screen is usually switched off when we
        // save (level transitions, autosaves), and the default search skips inactive objects.
        GauntletManager pManager = inv.primaryGauntlet.GetComponentInChildren<GauntletManager>(true);
        GauntletManager sManager = inv.secondaryGauntlet.GetComponentInChildren<GauntletManager>(true);

        if (pManager != null) primaryRarity = pManager.currentRarity;
        if (sManager != null) secondaryRarity = sManager.currentRarity;

        // 1. Clear old gems and save the new ones
        primaryGems.Clear();
        secondaryGems.Clear();

        ExtractGems(inv.primaryGauntlet, primaryGems);
        ExtractGems(inv.secondaryGauntlet, secondaryGems);

        // 2. Save Skill Gems
        primarySkillGemPrefab = ExtractSkillGem(pManager);
        secondarySkillGemPrefab = ExtractSkillGem(sManager);

        // 3. Level and EXP ride along with the gear, so they survive a level transition too.
        CaptureProgression();

        Debug.Log("Equipment successfully saved to the Backpack!");
    }

    #region Progression

    /// <summary>Reads the player's level and EXP off the EXP bar into the backpack.</summary>
    public void CaptureProgression()
    {
        ProgressBarCircle bar = ProgressBarCircle.Instance;
        if (bar == null) return;

        playerLevel = bar.currentLevel;
        playerExp = bar.currentExp;
        expToNextLevel = bar.expToNextLevel;
    }

    /// <summary>
    /// Pushes the stored level and EXP back onto the EXP bar. Called by the bar itself when it
    /// starts, so a bar spawned fresh in a new level picks up where the last one left off.
    /// </summary>
    public void ApplyProgression(ProgressBarCircle bar)
    {
        if (bar == null || !hasSavedData) return;

        bar.currentLevel = Mathf.Max(1, playerLevel);
        bar.currentExp = playerExp;
        if (expToNextLevel > 0f) bar.expToNextLevel = expToNextLevel;
    }

    #endregion

    private GameObject ExtractSkillGem(GauntletManager gm)
    {
        if (gm == null || gm.SkillSlot == null) return null;

        SkillSlotManager skillSlot = gm.SkillSlot.GetComponent<SkillSlotManager>();
        if (skillSlot == null) return null;

        // Ask the slot what it is really holding. Reading CurrentSkillGem directly would report a
        // skill gem the player had already dragged back out, which then reappeared on load.
        DraggableGem equipped = skillSlot.GetEquippedSkillGem();
        if (equipped == null || equipped.LinkedGemData == null) return null;

        GameObject prefab = FindPrefabForGemData(equipped.LinkedGemData);
        if (prefab == null)
        {
            Debug.LogError($"[PersistentEquipment] The equipped skill gem '{equipped.LinkedGemData.Name}' " +
                           "is not in the Master Gem Database, so it cannot be saved or carried between " +
                           "levels. Right-click this component and run 'Auto-Populate Prefab Databases'.");
        }

        return prefab;
    }

    /// <summary>Finds the project prefab whose DraggableGem points at the same GemData asset.</summary>
    private GameObject FindPrefabForGemData(GemData gemData)
    {
        if (gemData == null) return null;

        foreach (GameObject projectPrefab in masterGemDatabase)
        {
            if (projectPrefab == null) continue;

            DraggableGem prefabGem = projectPrefab.GetComponent<DraggableGem>();
            if (prefabGem != null && prefabGem.LinkedGemData == gemData) return projectPrefab;
        }

        return null;
    }

    private void ExtractGems(Transform gauntletParent, List<GameObject> gemList)
    {
        GauntletManager gm = gauntletParent.GetComponentInChildren<GauntletManager>(true);
        if (gm != null)
        {
            foreach (GameObject slot in gm.fingerSlots)
            {
                if (slot == null)
                {
                    gemList.Add(null);
                    continue;
                }

                DraggableGem gem = slot.GetComponentInChildren<DraggableGem>(true);

                if (gem != null && gem.LinkedGemData != null)
                {
                    // Match the equipped gem to our Master Database
                    GameObject prefab = FindPrefabForGemData(gem.LinkedGemData);

                    if (prefab == null)
                    {
                        Debug.LogError($"[PersistentEquipment] The equipped gem '{gem.LinkedGemData.Name}' " +
                                       "is not in the Master Gem Database, so it cannot be saved or carried " +
                                       "between levels. Right-click this component and run " +
                                       "'Auto-Populate Prefab Databases'.");
                    }

                    // A null keeps this slot's index aligned even when the lookup failed.
                    gemList.Add(prefab);
                }
                else
                {
                    // Empty slot, save a null space to keep index alignment perfect
                    gemList.Add(null);
                }
            }
        }
    }

    #region Disk Save Support

    /// <summary>Copies the backpack contents into a save payload as plain prefab names.</summary>
    public void WriteTo(GameSaveData data)
    {
        if (data == null) return;

        data.primaryGauntletName = PrefabName(primaryGauntletPrefab);
        data.primaryRarity = (int)primaryRarity;
        data.primaryGemNames = ToNames(primaryGems);
        data.primarySkillGemName = PrefabName(primarySkillGemPrefab);

        data.secondaryGauntletName = PrefabName(secondaryGauntletPrefab);
        data.secondaryRarity = (int)secondaryRarity;
        data.secondaryGemNames = ToNames(secondaryGems);
        data.secondarySkillGemName = PrefabName(secondarySkillGemPrefab);

        // Read the bar one last time so a checkpoint always stores the level as it stands.
        CaptureProgression();

        data.playerLevel = playerLevel;
        data.playerExp = playerExp;
        data.expToNextLevel = expToNextLevel;
    }

    /// <summary>
    /// Rebuilds the backpack from a save payload. InventoryManager then spawns the real
    /// gauntlets and gems from these prefab references exactly as it does between levels.
    /// </summary>
    public void ReadFrom(GameSaveData data)
    {
        if (data == null) return;

        hasSavedData = true;

        // The single most likely setup mistake: without this list nothing can be rebuilt, and the
        // player would load in with no gauntlets at all rather than an obvious error.
        if (masterGauntletDatabase == null || masterGauntletDatabase.Count == 0)
        {
            Debug.LogError("[PersistentEquipment] The Master Gauntlet Database is empty, so saved gear " +
                           "cannot be restored. Right-click this component and run " +
                           "'Auto-Populate Prefab Databases'.");
        }

        primaryGauntletPrefab = FindGauntletPrefab(data.primaryGauntletName);
        primaryRarity = ToRarity(data.primaryRarity);
        primaryGems = ToPrefabs(data.primaryGemNames);
        primarySkillGemPrefab = FindGemPrefab(data.primarySkillGemName);

        secondaryGauntletPrefab = FindGauntletPrefab(data.secondaryGauntletName);
        secondaryRarity = ToRarity(data.secondaryRarity);
        secondaryGems = ToPrefabs(data.secondaryGemNames);
        secondarySkillGemPrefab = FindGemPrefab(data.secondarySkillGemName);

        // The EXP bar reads these back out of here when it starts in the restored scene.
        playerLevel = Mathf.Max(1, data.playerLevel);
        playerExp = data.playerExp;
        if (data.expToNextLevel > 0f) expToNextLevel = data.expToNextLevel;

        ApplyProgression(ProgressBarCircle.Instance);
    }

    public GameObject FindGemPrefab(string prefabName)
    {
        return FindInDatabase(masterGemDatabase, prefabName, "gem");
    }

    public GameObject FindGauntletPrefab(string prefabName)
    {
        return FindInDatabase(masterGauntletDatabase, prefabName, "gauntlet");
    }

    private static GameObject FindInDatabase(List<GameObject> database, string prefabName, string label)
    {
        if (string.IsNullOrEmpty(prefabName) || database == null) return null;

        foreach (GameObject prefab in database)
        {
            if (prefab != null && prefab.name == prefabName) return prefab;
        }

        Debug.LogWarning($"[PersistentEquipment] Loaded a save that wants the {label} prefab '{prefabName}', " +
                         $"but it is not in the master {label} database. That slot will come back empty.");
        return null;
    }

    private static string PrefabName(GameObject prefab)
    {
        return prefab != null ? prefab.name : string.Empty;
    }

    private static List<string> ToNames(List<GameObject> prefabs)
    {
        List<string> names = new List<string>();
        if (prefabs == null) return names;

        // Empty slots are stored as empty strings so the slot indexes stay aligned.
        foreach (GameObject prefab in prefabs) names.Add(PrefabName(prefab));
        return names;
    }

    private List<GameObject> ToPrefabs(List<string> names)
    {
        List<GameObject> prefabs = new List<GameObject>();
        if (names == null) return prefabs;

        foreach (string name in names) prefabs.Add(FindGemPrefab(name));
        return prefabs;
    }

    private static GauntletRarity ToRarity(int value)
    {
        return System.Enum.IsDefined(typeof(GauntletRarity), value)
            ? (GauntletRarity)value
            : GauntletRarity.Common;
    }

    #endregion

#if UNITY_EDITOR
    /// <summary>
    /// Editor convenience: scans the project for every gem and gauntlet prefab so the two
    /// master databases don't have to be filled in by hand (and can't silently fall behind
    /// when someone adds a new gem).
    /// </summary>
    [ContextMenu("Auto-Populate Prefab Databases")]
    private void AutoPopulatePrefabDatabases()
    {
        List<GameObject> gems = new List<GameObject>();
        List<GameObject> gauntlets = new List<GameObject>();

        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            if (prefab.GetComponent<DraggableGem>() != null) gems.Add(prefab);
            else if (prefab.GetComponent<GauntletManager>() != null) gauntlets.Add(prefab);
        }

        masterGemDatabase = gems;
        masterGauntletDatabase = gauntlets;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[PersistentEquipment] Auto-populated {gems.Count} gem prefab(s) and {gauntlets.Count} gauntlet prefab(s).");
    }
#endif
}

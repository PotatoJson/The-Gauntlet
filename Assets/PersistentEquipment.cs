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

        GauntletManager pManager = inv.primaryGauntlet.GetComponentInChildren<GauntletManager>();
        GauntletManager sManager = inv.secondaryGauntlet.GetComponentInChildren<GauntletManager>();

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

        Debug.Log("Equipment successfully saved to the Backpack!");
    }

    private GameObject ExtractSkillGem(GauntletManager gm)
    {
        if (gm == null || gm.SkillSlot == null) return null;

        SkillSlotManager skillSlot = gm.SkillSlot.GetComponent<SkillSlotManager>();
        if (skillSlot != null && skillSlot.CurrentSkillGem != null)
        {
            // Match the equipped skill gem to our Master Database
            foreach (GameObject projectPrefab in masterGemDatabase)
            {
                DraggableGem prefabGem = projectPrefab.GetComponent<DraggableGem>();
                if (prefabGem != null && prefabGem.LinkedGemData == skillSlot.CurrentSkillGem.LinkedGemData)
                {
                    return projectPrefab;
                }
            }
        }
        return null;
    }

    private void ExtractGems(Transform gauntletParent, List<GameObject> gemList)
    {
        GauntletManager gm = gauntletParent.GetComponentInChildren<GauntletManager>();
        if (gm != null)
        {
            foreach (GameObject slot in gm.fingerSlots)
            {
                DraggableGem gem = slot.GetComponentInChildren<DraggableGem>();

                if (gem != null && gem.LinkedGemData != null)
                {
                    bool foundPrefab = false;

                    // Match the equipped gem to our Master Database
                    foreach (GameObject projectPrefab in masterGemDatabase)
                    {
                        if (projectPrefab.GetComponent<DraggableGem>().LinkedGemData == gem.LinkedGemData)
                        {
                            gemList.Add(projectPrefab);
                            foundPrefab = true;
                            break;
                        }
                    }

                    if (!foundPrefab) gemList.Add(null);
                }
                else
                {
                    // Empty slot, save a null space to keep index alignment perfect
                    gemList.Add(null);
                }
            }
        }
    }
}
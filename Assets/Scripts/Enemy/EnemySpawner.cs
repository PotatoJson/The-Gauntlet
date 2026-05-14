using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChamberData
{
    public string chamberName = "New Chamber";

    [Tooltip("If true, enemies will spawn immediately when the level starts. No trigger required.")]
    public bool spawnOnStart = false;

    [Tooltip("The trigger collider that the player walks into to start the spawn. Leave blank if Spawn On Start is true.")]
    public List<Collider> chamberTriggers = new List<Collider>();

    [Header("Spawn Point Containers")]
    [Tooltip("Parent GameObject containing all Grunt spawn points. Leave empty if none.")]
    public Transform gruntSpawnPoints;
    [Tooltip("Parent GameObject containing all Elite spawn points. Leave empty if none.")]
    public Transform eliteSpawnPoints;
    [Tooltip("Parent GameObject containing all Peeper spawn points. Leave empty if none.")]
    public Transform peeperSpawnPoints;
    [Tooltip("Parent GameObject containing all Caster spawn points. Leave empty if none.")]
    public Transform casterSpawnPoints;

    [Header("Boss Spawn (Single Point)")]
    [Tooltip("A single Transform for the Boss spawn point. Not a container. Leave empty if no boss in this chamber.")]
    public Transform bossSpawnPoint;

    [Header("Door Settings")]
    [Tooltip("The Animator for the door that opens when this chamber is cleared.")]
    public List<Animator> chamberDoorAnimators = new List<Animator>();
    public string doorOpenTrigger = "OpenDoor";

    // Internal State Tracking
    [HideInInspector] public bool hasSpawned = false;
    [HideInInspector] public bool isCleared = false;
    [HideInInspector] public List<GameObject> activeEnemies = new List<GameObject>();
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Global Enemy Prefabs")]
    public GameObject gruntPrefab;
    public GameObject elitePrefab;
    public GameObject peeperPrefab;
    public GameObject casterPrefab;
    public GameObject bossPrefab;

    [Header("Chambers Configuration")]
    [Tooltip("Add and configure your individual chambers here.")]
    public List<ChamberData> chambers = new List<ChamberData>();

    private void Start()
    {
        // Setup triggers for each chamber or spawn initial ones immediately
        for (int i = 0; i < chambers.Count; i++)
        {
            ChamberData chamber = chambers[i];

            if (chamber.spawnOnStart)
            {
                // Spawn immediately without waiting for a trigger
                SpawnChamberEnemies(chamber);
            }
            else if (chamber.chamberTriggers.Count > 0)
            {
                // Loop through every trigger assigned to this chamber
                foreach (Collider trigger in chamber.chamberTriggers)
                {
                    if (trigger != null)
                    {
                        // Ensure it's correctly marked as a trigger
                        trigger.isTrigger = true;

                        // Attach a helper component directly to the trigger object so it can detect player collision
                        ChamberTriggerListener listener = trigger.gameObject.AddComponent<ChamberTriggerListener>();
                        listener.Setup(this, i);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Chamber '{chamber.chamberName}' is missing a Trigger Collider and is not set to Spawn On Start!");
            }
        }
    }

    // Called by the ChamberTriggerListener when a player enters a chamber's trigger
    public void TriggerChamber(int chamberIndex, Collider other)
    {
        if (!other.CompareTag("Player")) return;

        ChamberData chamber = chambers[chamberIndex];

        // Ensure we only spawn once per chamber
        if (chamber.hasSpawned || chamber.isCleared) return;

        SpawnChamberEnemies(chamber);
    }

    private void SpawnChamberEnemies(ChamberData chamber)
    {
        chamber.hasSpawned = true;

        if (MetricsTracker.Instance != null)
        {
            // Updated to Unity 6 syntax: FindFirstObjectByType
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            
            // Now we just directly read CurrentHealth instead of doing math!
            int startingHealth = playerHealth != null ? Mathf.RoundToInt(playerHealth.CurrentHealth) : 100;
            
            MetricsTracker.Instance.StartChamber(chamber.chamberName, startingHealth);
        }

        // Spawn standard enemy types from their containers
        SpawnEnemyType(gruntPrefab, chamber.gruntSpawnPoints, chamber.activeEnemies);
        SpawnEnemyType(elitePrefab, chamber.eliteSpawnPoints, chamber.activeEnemies);
        SpawnEnemyType(peeperPrefab, chamber.peeperSpawnPoints, chamber.activeEnemies);
        SpawnEnemyType(casterPrefab, chamber.casterSpawnPoints, chamber.activeEnemies);

        // Spawn Boss explicitly at its single spawn point
        if (bossPrefab != null && chamber.bossSpawnPoint != null)
        {
            GameObject spawnedBoss = Instantiate(bossPrefab, chamber.bossSpawnPoint.position, chamber.bossSpawnPoint.rotation);
            chamber.activeEnemies.Add(spawnedBoss);
        }
    }

    private void SpawnEnemyType(GameObject prefab, Transform container, List<GameObject> enemyList)
    {
        // If there's no prefab assigned or the chamber doesn't use this enemy type, skip
        if (prefab == null || container == null) return;

        // If the container has no children, use the container itself as a single spawn point
        if (container.childCount == 0)
        {
            GameObject spawnedEnemy = Instantiate(prefab, container.position, container.rotation);
            enemyList.Add(spawnedEnemy);
        }
        else
        {
            // If it has children, iterate through them and spawn at each child's location
            foreach (Transform spawnPoint in container)
            {
                GameObject spawnedEnemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
                enemyList.Add(spawnedEnemy);
            }
        }
    }

    private void Update()
    {
        // Every frame, check active chambers to see if their enemies are defeated
        foreach (var chamber in chambers)
        {
            if (chamber.hasSpawned && !chamber.isCleared)
            {
                // Remove destroyed enemies (Unity treats destroyed GameObjects as null internally)
                chamber.activeEnemies.RemoveAll(enemy => enemy == null);

                // If zero enemies left, clear the chamber and open the door
                if (chamber.activeEnemies.Count == 0)
                {
                    ClearChamber(chamber, false);
                }
            }
        }
    }

    private void ClearChamber(ChamberData chamber, bool isInstantClear)
    {
        Debug.Log("Chamber cleared beginning test");
        chamber.isCleared = true;

        if (MetricsTracker.Instance != null)
        {
            // Updated to Unity 6 syntax: FindFirstObjectByType
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            
            // Now we just directly read CurrentHealth
            int endingHealth = playerHealth != null ? Mathf.RoundToInt(playerHealth.CurrentHealth) : 0;
            
            // Pass ending health, 'false' (because they survived), and "None" for the killer
            MetricsTracker.Instance.EndChamber(endingHealth, false, "None");
        }

        //Made changes for multiple gates to open.
        if (chamber.chamberDoorAnimators.Count > 0)
        {
            foreach (Animator doorAnim in chamber.chamberDoorAnimators)
            {
                if (doorAnim != null)
                {
                    doorAnim.SetTrigger(chamber.doorOpenTrigger);
                }
            }
        }
        else
        {
            Debug.LogWarning($"Chamber '{chamber.chamberName}' cleared, but no Door Animator is assigned!");
        }

        if (isInstantClear)
        {
            Debug.Log("Chamber cleared instantly");
            GrantChamberRewards();
        }
        else
        {
            Debug.Log("Chamber cleared starting...");
            StartCoroutine(ChamberRewardSequence());
        }
    }

    private IEnumerator ChamberRewardSequence()
    {
        yield return new WaitForSeconds(2f);
        GrantChamberRewards();
    }

    private void GrantChamberRewards()
    {
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if(playerHealth != null)
        {
            if(playerHealth.CurrentPotions < playerHealth.MaxPotions)
            {
                playerHealth.CurrentPotions++;
                Debug.Log($"Chamber Cleared! Potion refilled. Total: {playerHealth.CurrentPotions}");
            }
        }

        if(RewardMenuManager.Instance != null)
        {
            RewardMenuManager.Instance.OpenRewardMenu();
        }
        else Debug.Log("RewardManager not found");
    }

    public void ForceClearChamber(int chamberIndex)
    {
        // Safety check to make sure the index actually exists
        if (chamberIndex < 0 || chamberIndex >= chambers.Count) return;

        ChamberData chamber = chambers[chamberIndex];

        // Only clear it if it hasn't been cleared already
        if (!chamber.isCleared)
        {
            ClearChamber(chamber, true);
        }
    }
}


// -----------------------------------------------------------------------------------------
// Helper Component: Automatically attached to each trigger at runtime.
// It catches standard Unity Rigidbody physics triggered by the player.
// -----------------------------------------------------------------------------------------
public class ChamberTriggerListener : MonoBehaviour
{
    private EnemySpawner manager;
    private int chamberIndex;

    public void Setup(EnemySpawner manager, int chamberIndex)
    {
        this.manager = manager;
        this.chamberIndex = chamberIndex;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager != null)
        {
            manager.TriggerChamber(chamberIndex, other);
        }
    }

    
}
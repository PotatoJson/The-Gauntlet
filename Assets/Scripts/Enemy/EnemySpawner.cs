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
    [Tooltip("Parent GameObject containing all Shield spawn points. Leave empty if none.")]
    public Transform shieldSpawnPoints;
    [Tooltip("Parent GameObject containing all Spinning spawn points. Leave empty if none.")]
    public Transform spinningSpawnPoints;
    [Tooltip("Parent GameObject containing all Biter spawn points. Leave empty if none.")]
    public Transform biterSpawnPoints;

    [Header("Boss Spawn (Single Point)")]
    [Tooltip("A single Transform for the Boss spawn point. Not a container. Leave empty if no boss in this chamber.")]
    public Transform bossSpawnPoint;

    [Header("Door Settings")]
    [Tooltip("The Animator for the door that opens when this chamber is cleared.")]
    public List<Animator> chamberDoorAnimators = new List<Animator>();
    public string doorOpenTrigger = "OpenDoor";

    [Header("Chamber Reward")]
    [Tooltip("Drag the deactivated ChamberRewardTrigger for this room here.")]
    public GameObject chamberRewardObject;

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
    public GameObject shieldPrefab;
    public GameObject spinningPrefab;
    public GameObject biterPrefab;
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
                        listener.spawner = this;
                        listener.chamberToSpawn = chamber;
                    }
                }
            }
        }
    }

    private void FixedUpdate()
    {
        // Continuously check active encounters to see if they're cleared
        foreach (ChamberData chamber in chambers)
        {
            if (chamber.hasSpawned && !chamber.isCleared)
            {
                CheckChamberStatus(chamber);
            }
        }
    }

    public void OnChamberTriggerEntered(ChamberData chamber)
    {
        // The listener will call this when the player steps into the trigger
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
        SpawnEnemyType(shieldPrefab, chamber.shieldSpawnPoints, chamber.activeEnemies);
        SpawnEnemyType(spinningPrefab, chamber.spinningSpawnPoints, chamber.activeEnemies);
        SpawnEnemyType(biterPrefab, chamber.biterSpawnPoints, chamber.activeEnemies);

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

    private void CheckChamberStatus(ChamberData chamber)
    {
        // Clean out any destroyed enemies from the list (since they get Destroy()'d on death)
        chamber.activeEnemies.RemoveAll(enemy => enemy == null);

        // If there are no active enemies left, the chamber is complete
        if (chamber.activeEnemies.Count == 0)
        {
            chamber.isCleared = true;
            OnChamberCleared(chamber);
        }
    }

    private void OnChamberCleared(ChamberData chamber)
    {
        Debug.Log($"<color=green>Chamber Cleared: {chamber.chamberName}</color>");

        // Clearing a room IS the checkpoint. This is the only place the game saves.
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.RecordCheckpoint($"cleared '{chamber.chamberName}'");
        }

        if (MetricsTracker.Instance != null)
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            int endingHealth = playerHealth != null ? Mathf.RoundToInt(playerHealth.CurrentHealth) : 100;

            // Pass required 'died' and optional 'enemyName' arguments
            MetricsTracker.Instance.EndChamber(endingHealth, false, "None");
        }

        // Open the associated doors
        foreach (Animator doorAnim in chamber.chamberDoorAnimators)
        {
            if (doorAnim != null)
            {
                doorAnim.SetTrigger(chamber.doorOpenTrigger);
            }
        }

        // Enable the reward object
        if (chamber.chamberRewardObject != null)
        {
            chamber.chamberRewardObject.SetActive(true);
        }
    }
    // Add this method to the EnemySpawner class
    public void ForceClearChamber(int chamberIndex)
    {
        if (chamberIndex < 0 || chamberIndex >= chambers.Count)
        {
            Debug.LogError($"ForceClearChamber: Invalid chamber index {chamberIndex}.");
            return;
        }

        ChamberData chamber = chambers[chamberIndex];
        // Clear all enemies in the chamber
        foreach (GameObject enemy in chamber.activeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        chamber.activeEnemies.Clear();

        // Mark the chamber as cleared
        chamber.isCleared = true;

        // Open doors or perform any additional logic for a cleared chamber
        OnChamberCleared(chamber);
    }

    /// <summary>
    /// Re-applies a chamber's cleared state from a save file: despawns anything that already
    /// spawned, seals the room so it never spawns again, and opens its doors. Deliberately
    /// does NOT re-run OnChamberCleared, because that would fire duplicate telemetry.
    /// </summary>
    /// <param name="chamberName">The ChamberData name recorded in the save.</param>
    /// <param name="rewardStillAvailable">
    /// True if the reward was still on the floor when the player saved, so it should be put back.
    /// </param>
    public bool RestoreChamberState(string chamberName, bool rewardStillAvailable)
    {
        foreach (ChamberData chamber in chambers)
        {
            if (chamber.chamberName != chamberName) continue;

            foreach (GameObject enemy in chamber.activeEnemies)
            {
                if (enemy != null) Destroy(enemy);
            }
            chamber.activeEnemies.Clear();

            chamber.hasSpawned = true;
            chamber.isCleared = true;

            // Switch the entry triggers off so walking back through them does nothing.
            foreach (Collider trigger in chamber.chamberTriggers)
            {
                if (trigger != null) trigger.enabled = false;
            }

            foreach (Animator doorAnim in chamber.chamberDoorAnimators)
            {
                if (doorAnim != null) doorAnim.SetTrigger(chamber.doorOpenTrigger);
            }

            if (chamber.chamberRewardObject != null)
            {
                chamber.chamberRewardObject.SetActive(rewardStillAvailable);
            }

            Debug.Log($"[EnemySpawner] Restored cleared chamber '{chamberName}' " +
                      $"(reward available: {rewardStillAvailable}).");
            return true;
        }

        return false;
    }

    /// <summary>Chamber names this spawner currently considers cleared, for the save file.</summary>
    public void CollectClearedChambers(List<string> clearedNames, List<string> unclaimedRewardNames)
    {
        foreach (ChamberData chamber in chambers)
        {
            if (!chamber.isCleared || string.IsNullOrEmpty(chamber.chamberName)) continue;

            if (!clearedNames.Contains(chamber.chamberName)) clearedNames.Add(chamber.chamberName);

            bool rewardWaiting = chamber.chamberRewardObject != null && chamber.chamberRewardObject.activeSelf;
            if (rewardWaiting && !unclaimedRewardNames.Contains(chamber.chamberName))
            {
                unclaimedRewardNames.Add(chamber.chamberName);
            }
        }
    }
}


// -----------------------------------------------------------------------------------------
// Helper Component: Automatically attached to each trigger at runtime.
// It catches standard Unity Rigidbody physics triggered by the player.
// -----------------------------------------------------------------------------------------
public class ChamberTriggerListener : MonoBehaviour
{
    public EnemySpawner spawner;
    public ChamberData chamberToSpawn;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && spawner != null && chamberToSpawn != null)
        {
            spawner.OnChamberTriggerEntered(chamberToSpawn);
        }
    }
}


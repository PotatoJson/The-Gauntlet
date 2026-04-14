using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChamberData
{
    public string chamberName = "New Chamber";

    [Tooltip("If true, enemies will spawn immediately when the level starts. No trigger required.")]
    public bool spawnOnStart = false;

    [Tooltip("The trigger collider that the player walks into to start the spawn. Leave blank if Spawn On Start is true.")]
    public Collider chamberTrigger;

    [Header("Spawn Point Containers")]
    [Tooltip("Parent GameObject containing all Grunt spawn points. Leave empty if none.")]
    public Transform gruntSpawnPoints;
    [Tooltip("Parent GameObject containing all Elite spawn points. Leave empty if none.")]
    public Transform eliteSpawnPoints;
    [Tooltip("Parent GameObject containing all Peeper spawn points. Leave empty if none.")]
    public Transform peeperSpawnPoints;

    [Header("Door Settings")]
    [Tooltip("The Animator for the door that opens when this chamber is cleared.")]
    public Animator chamberDoorAnimator;
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
            else if (chamber.chamberTrigger != null)
            {
                // Ensure it's correctly marked as a trigger
                chamber.chamberTrigger.isTrigger = true;

                // Attach a helper component directly to the trigger object so it can detect player collision
                ChamberTriggerListener listener = chamber.chamberTrigger.gameObject.AddComponent<ChamberTriggerListener>();
                listener.Setup(this, i);
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

        // Spawn all 3 types (if a container is left blank, the method just skips it)
        SpawnEnemyType(gruntPrefab, chamber.gruntSpawnPoints, chamber.activeEnemies);
        SpawnEnemyType(elitePrefab, chamber.eliteSpawnPoints, chamber.activeEnemies);
        SpawnEnemyType(peeperPrefab, chamber.peeperSpawnPoints, chamber.activeEnemies);
    }

    private void SpawnEnemyType(GameObject prefab, Transform container, List<GameObject> enemyList)
    {
        // If there's no prefab assigned or the chamber doesn't use this enemy type, skip
        if (prefab == null || container == null) return;

        foreach (Transform spawnPoint in container)
        {
            GameObject spawnedEnemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            enemyList.Add(spawnedEnemy);
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
                    ClearChamber(chamber);
                }
            }
        }
    }

    private void ClearChamber(ChamberData chamber)
    {
        chamber.isCleared = true;

        if (chamber.chamberDoorAnimator != null)
        {
            chamber.chamberDoorAnimator.SetTrigger(chamber.doorOpenTrigger);
        }
        else
        {
            Debug.LogWarning($"Chamber '{chamber.chamberName}' cleared, but no Door Animator is assigned!");
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
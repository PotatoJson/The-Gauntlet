using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the single checkpoint save. It bootstraps itself, so no scene needs to contain it and
/// no prefab needs wiring up: the first scene that loads creates it and it survives from there.
///
/// The save is written at exactly one moment: when a chamber is cleared. Nothing else writes it.
/// It exists so a player who quits mid-session can be put back where they were, so there is no
/// manual save and no load menu; the main menu's Play button resumes it.
///
/// Dying deletes the save (see PlayerHealth.HandleDeath), which is what stops a resume from
/// being used to undo a death.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    /// <summary>Raised after the save is written or deleted, so the main menu can refresh.</summary>
    public static event System.Action OnSaveStateChanged;

    /// <summary>
    /// Raised only when a checkpoint is actually written to disk. This is what drives the
    /// on-screen "Auto-saving" indicator, so it must not fire on deletes or skipped saves.
    /// </summary>
    public static event System.Action OnCheckpointSaved;

    private readonly List<string> _visitedLevels = new List<string>();
    private string _currentSceneName;
    private bool _isRestoring;
    private bool _checkpointQueued;

    public bool HasSave => SaveSystem.HasSave();

    /// <summary>True while a save is being applied. Checkpoints are ignored during this.</summary>
    public bool IsRestoring => _isRestoring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject host = new GameObject("[SaveManager]");
        host.AddComponent<SaveManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    // Only tracks where we are. Changing level is deliberately NOT a checkpoint.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _currentSceneName = scene.name;
    }

    #region Checkpoints

    /// <summary>
    /// The one and only way a save gets written. Called by EnemySpawner when a chamber is cleared.
    ///
    /// The write is deferred to the end of the frame because the caller is still changing the
    /// world when it fires (opening doors, spawning the reward), and the snapshot needs their
    /// final state.
    /// </summary>
    public void RecordCheckpoint(string reason)
    {
        if (_isRestoring || _checkpointQueued) return;

        _checkpointQueued = true;
        StartCoroutine(WriteCheckpointAtEndOfFrame(reason));
    }

    private IEnumerator WriteCheckpointAtEndOfFrame(string reason)
    {
        yield return null;

        _checkpointQueued = false;
        if (!_isRestoring) WriteCheckpoint(reason);
    }

    private bool WriteCheckpoint(string reason)
    {
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health == null)
        {
            Debug.Log($"[SaveManager] Skipped the checkpoint ({reason}): no player in '{_currentSceneName}'.");
            return false;
        }

        if (health.IsDead)
        {
            Debug.Log($"[SaveManager] Skipped the checkpoint ({reason}): the player is dead.");
            return false;
        }

        if (!_visitedLevels.Contains(_currentSceneName)) _visitedLevels.Add(_currentSceneName);

        GameSaveData data = Capture(health);
        bool written = SaveSystem.Write(data);

        if (written)
        {
            Debug.Log($"[SaveManager] Checkpoint reached ({reason}): {data.GetSummary()}");
            OnSaveStateChanged?.Invoke();
            OnCheckpointSaved?.Invoke();
        }

        return written;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Resumes the checkpoint. The saved scene is always reloaded, so enemies, doors and props
    /// start from a known state before the snapshot is applied on top.
    /// </summary>
    public bool ResumeFromCheckpoint()
    {
        if (_isRestoring)
        {
            Debug.LogWarning("[SaveManager] A resume is already in progress.");
            return false;
        }

        if (!SaveSystem.TryRead(out GameSaveData data))
        {
            Debug.LogWarning("[SaveManager] There is no checkpoint to resume.");
            return false;
        }

        StartCoroutine(RestoreRoutine(data));
        return true;
    }

    /// <summary>Clears the run tracking and the save file. Call this when starting a fresh run.</summary>
    public void StartNewRun()
    {
        _visitedLevels.Clear();
        SaveSystem.Delete();
        OnSaveStateChanged?.Invoke();
    }

    /// <summary>Discards the save. Called on death so a run cannot be resumed past it.</summary>
    public void DeleteSave()
    {
        SaveSystem.Delete();
        OnSaveStateChanged?.Invoke();
    }

    /// <summary>Reads the save without applying it, for a summary line on the main menu.</summary>
    public GameSaveData PeekSave() => SaveSystem.Peek();

    #endregion

    #region Capture

    private GameSaveData Capture(PlayerHealth health)
    {
        GameSaveData data = new GameSaveData
        {
            sceneName = _currentSceneName,
            visitedLevels = new List<string>(_visitedLevels),
        };

        // --- Stats ---
        data.currentHealth = health.CurrentHealth;
        data.maxHealthAtSave = health.MaxHealth;
        data.currentPoise = health.CurrentPoise;
        data.currentPotions = health.CurrentPotions;

        PlayerStamina stamina = health.GetComponent<PlayerStamina>();
        if (stamina == null) stamina = FindFirstObjectByType<PlayerStamina>();
        if (stamina != null) data.currentStamina = stamina.CurrentStamina;

        // --- Position ---
        Transform playerTransform = health.transform;
        data.SetPosition(playerTransform.position);
        data.playerYaw = playerTransform.eulerAngles.y;

        // Keep the telemetry run ID so quitting and resuming stays one run in the sheet.
        if (MetricsTracker.Instance != null) data.runID = MetricsTracker.Instance.CurrentRunID;

        // --- Chamber progress ---
        EnemySpawner[] spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
        foreach (EnemySpawner spawner in spawners)
        {
            spawner.CollectClearedChambers(data.clearedChambers, data.chambersWithUnclaimedReward);
        }

        // --- Equipment ---
        if (PersistentEquipment.Instance != null)
        {
            // Refresh the backpack from the live UI first so the snapshot matches what the
            // player is actually wearing, then copy it out as prefab names.
            if (InventoryManager.Instance != null)
            {
                PersistentEquipment.Instance.SaveEquipment(InventoryManager.Instance);
            }

            PersistentEquipment.Instance.WriteTo(data);
        }
        else
        {
            Debug.LogWarning("[SaveManager] No PersistentEquipment in the scene, so gear will not be saved.");
        }

        return data;
    }

    #endregion

    #region Restore

    private IEnumerator RestoreRoutine(GameSaveData data)
    {
        _isRestoring = true;
        Time.timeScale = 1f;

        _visitedLevels.Clear();
        if (data.visitedLevels != null) _visitedLevels.AddRange(data.visitedLevels);

        if (MetricsTracker.Instance != null) MetricsTracker.Instance.ResumeRun(data.runID);

        // 1. Load the saved level from scratch.
        AsyncOperation load = SceneManager.LoadSceneAsync(data.sceneName);
        if (load == null)
        {
            Debug.LogError($"[SaveManager] Could not load the scene '{data.sceneName}'. " +
                           "Is it in the Build Settings scene list?");
            _isRestoring = false;
            yield break;
        }

        while (!load.isDone) yield return null;

        // Let every Awake/Start in the fresh scene run before touching anything.
        //
        // This wait is also what brings the Backpack (PersistentEquipment) into existence when
        // resuming from the main menu: the Backpack lives in the gameplay scenes, so there is no
        // instance to write the gear into until after this point. Writing it any earlier silently
        // dropped the entire loadout and the player spawned with default gauntlets.
        yield return null;

        // 2. Gear: fill the backpack from the save, then rebuild the character screen from it.
        if (PersistentEquipment.Instance == null)
        {
            Debug.LogError($"[SaveManager] '{data.sceneName}' has no PersistentEquipment (Backpack) " +
                           "object, so the saved gear cannot be restored. Every level that can be " +
                           "resumed into needs the Backpack prefab in it.");
        }
        else
        {
            PersistentEquipment.Instance.ReadFrom(data);

            // This also recalculates max health/stamina from the restored gems.
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ReloadFromBackpack();
            }
            else
            {
                Debug.LogError($"[SaveManager] '{data.sceneName}' has no InventoryManager, " +
                               "so the saved gear could not be put back on.");
            }
        }

        // Give the OnStatsCalculated listeners a frame to apply the new maximums.
        yield return null;

        // 3. Vitals, now that the maximums are correct.
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            health.RestoreState(data.currentHealth, data.currentPoise, data.currentPotions);
        }
        else
        {
            Debug.LogWarning($"[SaveManager] No PlayerHealth in '{data.sceneName}', so vitals were not restored.");
        }

        PlayerStamina stamina = FindFirstObjectByType<PlayerStamina>();
        if (stamina != null) stamina.RestoreState(data.currentStamina);

        // 4. Position. This runs last so it overrides the PlayerSpawn teleport that
        //    PlayerPersistence performs on every scene load.
        if (health != null) health.TeleportTo(data.GetPosition(), data.playerYaw);

        // 5. Chamber progress.
        RestoreChambers(data);

        _currentSceneName = data.sceneName;
        if (!_visitedLevels.Contains(_currentSceneName)) _visitedLevels.Add(_currentSceneName);

        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _isRestoring = false;
        Debug.Log($"[SaveManager] Resumed: {data.GetSummary()}");
    }

    private void RestoreChambers(GameSaveData data)
    {
        if (data.clearedChambers == null || data.clearedChambers.Count == 0) return;

        EnemySpawner[] spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
        if (spawners.Length == 0)
        {
            Debug.LogWarning($"[SaveManager] '{data.sceneName}' has no EnemySpawner, " +
                             "so cleared-chamber progress could not be restored.");
            return;
        }

        foreach (string chamberName in data.clearedChambers)
        {
            bool rewardStillAvailable = data.chambersWithUnclaimedReward != null &&
                                        data.chambersWithUnclaimedReward.Contains(chamberName);

            bool matched = false;
            foreach (EnemySpawner spawner in spawners)
            {
                if (spawner.RestoreChamberState(chamberName, rewardStillAvailable)) matched = true;
            }

            if (!matched)
            {
                Debug.LogWarning($"[SaveManager] The save lists a cleared chamber '{chamberName}' " +
                                 $"that does not exist in '{data.sceneName}'. It was skipped.");
            }
        }
    }

    #endregion
}

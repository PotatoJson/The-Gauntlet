using Steamworks;
using UnityEngine;

/// <summary>
/// Keeps the Steam connection open and unlocks achievements. Creates itself, so no scene needs it. When Steam isn't
/// running every call quietly does nothing, so the game plays the same without it.
///
/// The IDs must match the API names in Steamworks App Admin > Stats &amp; Achievements.
/// </summary>
public class SteamAchievements : MonoBehaviour
{
    public const string FirstMerge = "FIRST_MERGE";
    public const string MaxTierGem = "MAX_TIER_GEM";
    public const string UltraRareGauntlet = "ULTRARARE_GAUNTLET";
    public const string BossDemon = "BOSS_DEMON";
    public const string BossHellLord = "BOSS_HELL_LORD";
    public const string FlawlessChamber = "FLAWLESS_CHAMBER";
    public const string RunComplete = "RUN_COMPLETE";

    private const uint AppId = 4694770;

    private static SteamAchievements _instance;
    private static bool _ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        GameObject host = new GameObject("[SteamAchievements]");
        _instance = host.AddComponent<SteamAchievements>();
        DontDestroyOnLoad(host);
    }

    /// <summary>Unlocks an achievement once; Steam shows its own popup.</summary>
    public static void Unlock(string id)
    {
        Debug.Log($"[Steam] Achievement earned: {id} (Steam running: {_ready})");
        if (!_ready) return;

        if (SteamUserStats.GetAchievement(id, out bool alreadyUnlocked) && alreadyUnlocked) return;

        if (SteamUserStats.SetAchievement(id)) SteamUserStats.StoreStats();
        else Debug.LogWarning($"[Steam] Steam doesn't know the achievement {id}. Add and publish it in App Admin.");
    }

    private void Awake()
    {
        try
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Started from the .exe rather than from Steam: hand over to Steam so overlay and achievements work. Not in
            // development builds, so that a local test build can be launched straight from its folder.
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
            {
                Application.Quit();
                return;
            }
#endif
            _ready = SteamAPI.Init();
            Debug.Log($"[Steam] Connected: {_ready}");
        }
        catch (System.DllNotFoundException e)
        {
            Debug.LogWarning($"[Steam] steam_api library is missing, so Steam features are off: {e.Message}");
        }
    }

    private void Update()
    {
        if (_ready) SteamAPI.RunCallbacks();
    }

    private void OnDestroy()
    {
        if (_ready) SteamAPI.Shutdown();
        _ready = false;
        _instance = null;
    }
}

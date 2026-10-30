using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Playtesters could spend minutes hunting for the last few enemies of a room. When only a handful are left and
/// none has died for a while, this switches on the "EnemyHint" renderer feature, which draws every enemy as a glowing
/// silhouette through walls (see Hellbent/EnemyXRay). It goes off as soon as the player has an enemy in sight, and the
/// wait starts over, so it only comes back if they lose it and spend another full wait searching.
///
/// It lives on a prefab in Resources that creates itself once at startup, so no scene needs wiring.
/// </summary>
public class LastEnemiesHint : MonoBehaviour
{
    private const string FeatureName = "EnemyHint";
    private const string WallLayer = "Enviroment"; // spelled this way in the project's layer list

    [SerializeField] private UniversalRendererData rendererData;
    [Tooltip("The hint can only start once this many enemies (or fewer) are left in the active rooms.")]
    [SerializeField] private int maxRemaining = 4;
    [Tooltip("Seconds without a kill, while few enough are left, before the hint turns on.")]
    [SerializeField] private float searchSeconds = 25f;

    private ScriptableRendererFeature _feature;
    private EnemySpawner _spawner;
    private float _tick;
    private float _idle;
    private int _lastCount = -1;
    private bool _on;
    private bool _forced; // debug menu: show regardless of the count

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<LastEnemiesHint>() != null) return;

        GameObject prefab = Resources.Load<GameObject>("LastEnemiesHint");
        if (prefab == null) return;

        GameObject host = Instantiate(prefab);
        host.name = "[LastEnemiesHint]";
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (rendererData != null) _feature = rendererData.rendererFeatures.Find(f => f != null && f.name == FeatureName);
        SetHint(false);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SetHint(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _spawner = null;
        _lastCount = -1;
        _idle = 0f;
        SetHint(false);
    }

    private void Update()
    {
        if (_feature == null || _forced) return;

        _tick -= Time.deltaTime;
        if (_tick > 0f) return;
        _tick = 0.5f;

        if (_spawner == null)
        {
            _spawner = FindFirstObjectByType<EnemySpawner>();
            if (_spawner == null) return;
        }

        int remaining = CountRemaining(out bool inSight);

        // Any kill (or a new wave spawning) restarts the countdown, and so does having an enemy in sight: the
        // player isn't searching then.
        if (remaining != _lastCount || inSight) _idle = 0f;
        else _idle += 0.5f;
        _lastCount = remaining;

        if (remaining == 0 || remaining > maxRemaining || inSight) SetHint(false);
        else if (!_on && _idle >= searchSeconds) SetHint(true);
    }

    // Enemies in rooms that have spawned and are not yet cleared. A dying enemy lingers briefly before it is destroyed.
    private int CountRemaining(out bool inSight)
    {
        int count = 0;
        inSight = false;
        Camera view = Camera.main;

        foreach (ChamberData chamber in _spawner.chambers)
        {
            if (!chamber.hasSpawned || chamber.isCleared) continue;

            foreach (GameObject enemy in chamber.activeEnemies)
            {
                if (enemy == null) continue;

                BaseEnemy baseEnemy = enemy.GetComponent<BaseEnemy>();
                if (baseEnemy != null && baseEnemy.IsDead()) continue;

                count++;
                if (!inSight && view != null) inSight = IsInSight(view, enemy);
            }
        }

        return count;
    }

    // On screen with no wall in between, aimed at the same point the lock-on uses.
    private static bool IsInSight(Camera view, GameObject enemy)
    {
        Collider body = enemy.GetComponent<Collider>();
        Vector3 point = body != null
            ? body.bounds.center + Vector3.up * (body.bounds.extents.y * 0.5f)
            : enemy.transform.position + Vector3.up;

        Vector3 onScreen = view.WorldToViewportPoint(point);
        if (onScreen.z <= 0f || onScreen.x < 0f || onScreen.x > 1f || onScreen.y < 0f || onScreen.y > 1f) return false;

        return !Physics.Linecast(view.transform.position, point, LayerMask.GetMask(WallLayer), QueryTriggerInteraction.Ignore);
    }

    /// <summary>Debug menu: flips the outline on or off no matter how many enemies are left.</summary>
    public static void DebugToggle()
    {
        LastEnemiesHint hint = FindFirstObjectByType<LastEnemiesHint>();
        if (hint == null) return;

        hint._forced = !hint._on;
        hint.SetHint(hint._forced);
    }

    private void SetHint(bool on)
    {
        _on = on;
        if (_feature != null && _feature.isActive != on) _feature.SetActive(on);
    }
}

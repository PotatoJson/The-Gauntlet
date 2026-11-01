using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The screen between two levels: the hero chasing a few demons for a couple of seconds, then the next level loads
/// behind it. The player, camera and HUD survive scene loads, so this covers them rather than being a scene of its
/// own; a real scene in between would leave the player falling through empty space. Gameplay stays frozen under it,
/// and to everything else in the game the levels still follow each other directly.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    private const string PrefabPath = "LoadingScreen/LoadingScreen";
    private const float RunFrameSeconds = 0.095f;
    private const float SpinnerFrameSeconds = 0.09f;
    private const float DotSeconds = 0.4f;

    [SerializeField] private float seconds = 2.5f;
    [SerializeField] private float fadeOutSeconds = 0.6f;

    [SerializeField] private Sprite[] heroFrames;
    [SerializeField] private Sprite[] mobFrames;
    [SerializeField] private Sprite[] spinnerFrames;

    [SerializeField] private Image hero;
    [SerializeField] private Image[] mobs;
    [SerializeField] private Image spinner;
    [SerializeField] private RawImage ground;
    [SerializeField] private TMP_Text label;

    [Tooltip("How far the chasers sway back and forth as they run, in canvas units.")]
    [SerializeField] private float swayDistance = 26f;
    [SerializeField] private float groundScrollSpeed = 2.4f;

    private static bool _loading;

    private Vector2 _heroHome;
    private Vector2[] _mobHomes;
    private string _labelText;

    /// <summary>Shows the screen and loads this level behind it.</summary>
    public static void LoadLevel(string sceneName)
    {
        if (_loading) return;

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        LoadingScreen screen = Instantiate(prefab).GetComponent<LoadingScreen>();
        screen.StartCoroutine(screen.Load(sceneName));
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        _heroHome = hero.rectTransform.anchoredPosition;
        _mobHomes = new Vector2[mobs.Length];
        for (int i = 0; i < mobs.Length; i++) _mobHomes[i] = mobs[i].rectTransform.anchoredPosition;

        _labelText = new LocalizedString("UIStrings", "Loading").GetLocalizedString();
    }

    private IEnumerator Load(string sceneName)
    {
        _loading = true;

        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.Suspend(this); // released by the scene load

        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            Time.timeScale = 0f; // every frame: a hit-stop that was running when the level ended would undo it
            yield return null;
        }

        // The level loads last: loading stalls the main thread, which would freeze the animation if it came first.
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (load != null && !load.isDone) yield return null;

        Time.timeScale = 1f;
        _loading = false;

        yield return null; // the new level's first frame stays covered, and its long load time isn't counted below

        CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
        for (float faded = 0f; faded < fadeOutSeconds; faded += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - faded / fadeOutSeconds;
            yield return null;
        }

        Destroy(gameObject);
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        int runFrame = Mathf.FloorToInt(t / RunFrameSeconds);

        hero.sprite = heroFrames[runFrame % heroFrames.Length];
        hero.rectTransform.anchoredPosition = _heroHome + Vector2.right * (Mathf.Sin(t * 1.7f) * swayDistance);

        for (int i = 0; i < mobs.Length; i++)
        {
            mobs[i].sprite = mobFrames[(runFrame + i * 3) % mobFrames.Length];
            mobs[i].rectTransform.anchoredPosition = _mobHomes[i] + Vector2.right * (Mathf.Sin(t * 2.3f + i * 2.1f) * swayDistance);
        }

        spinner.sprite = spinnerFrames[Mathf.FloorToInt(t / SpinnerFrameSeconds) % spinnerFrames.Length];

        Rect uv = ground.uvRect;
        uv.x = t * groundScrollSpeed % 1f;
        ground.uvRect = uv;

        label.text = _labelText + new string('.', Mathf.FloorToInt(t / DotSeconds) % 4);
    }
}

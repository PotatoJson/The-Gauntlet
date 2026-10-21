using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The one place for every debug / cheat action. It used to be a pile of single-key shortcuts (N, T, M, K, `,
/// +, -, P, 9...) that playtesters kept hitting by accident; now there is a single key, 0.
///
/// Press 0 to cycle: hidden -> open (cursor free, game input off, click the buttons) -> minimised (just a small
/// tag at the top-right corner, game playable again) -> hidden. The tag's [-] / [+] button does the same as
/// moving between open and minimised. The menu is built from code, so there is no prefab to keep in sync.
/// </summary>
public class DebugHUD : MonoBehaviour
{
    [Header("Legacy references (kept so scenes don't lose their wiring)")]
    [Tooltip("The old five-button panel. It is hidden for good; the new menu replaces it.")]
    public GameObject debugMenuPanel;
    public InventoryManager inventoryManager;
    public PlayerStatsManager statsManager;

    [Tooltip("One 'Spawn gauntlet' button per prefab (the five elemental gauntlets).")]
    public GameObject[] gauntletPrefabs = new GameObject[0];

    [Header("Test Gems")]
    [Tooltip("Slotted into both gauntlets' skill sockets by the 'Equip test skill gem' button.")]
    public SkillGemData GenericTestGem;

    private enum State { Hidden, Open, Minimised }

    private State _state = State.Hidden;
    private RectTransform _root;
    private RectTransform _body;
    private TMP_Text _toggleLabel;
    private TMP_FontAsset _font;
    private bool _built;
    private bool _holdingInput;

    private void Start()
    {
        if (debugMenuPanel != null) debugMenuPanel.SetActive(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
        {
            Apply(_state == State.Hidden ? State.Open : _state == State.Open ? State.Minimised : State.Hidden);
        }
    }

    private void OnDisable()
    {
        ReleaseInput();
    }

    #region State
    private void Apply(State next)
    {
        if (!_built) Build();
        if (_root == null) return;

        _state = next;
        _root.gameObject.SetActive(next != State.Hidden);
        _body.gameObject.SetActive(next == State.Open);
        _toggleLabel.text = next == State.Open ? "-" : "+";

        if (next == State.Open) HoldInput();
        else ReleaseInput();

        if (next == State.Open) Refresh();
    }

    // While the menu is open the player needs a cursor and must not punch when clicking a button.
    private void HoldInput()
    {
        if (_holdingInput) return;
        _holdingInput = true;

        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ReleaseInput()
    {
        if (!_holdingInput) return;
        _holdingInput = false;

        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);

        // Back to the game's cursor, unless a real menu is up (they freeze time).
        if (Time.timeScale > 0f)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    #endregion

    #region Building the UI
    private void Build()
    {
        _built = true;

        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        _font = inventoryManager != null && inventoryManager.DetailNameText != null ? inventoryManager.DetailNameText.font : TMP_Settings.defaultFontAsset;

        var rootGo = new GameObject("DebugMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _root = (RectTransform)rootGo.transform;
        _root.SetParent(canvas.transform, false);
        _root.SetAsLastSibling();
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(1f, 1f);
        _root.anchoredPosition = new Vector2(-12f, -12f);
        _root.sizeDelta = new Vector2(300f, 0f);

        rootGo.GetComponent<Image>().color = new Color(0.07f, 0.06f, 0.09f, 0.92f);

        var layout = rootGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        rootGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildHeader();

        var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup));
        _body = (RectTransform)bodyGo.transform;
        _body.SetParent(_root, false);
        var bodyLayout = bodyGo.GetComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 4f;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        BuildSection("Levels", new List<(string, UnityEngine.Events.UnityAction)>
        {
            ("Next level", DebugActions.NextLevel),
            ("Reload scene", DebugActions.ReloadScene),
            ("Dungeon", () => DebugActions.LoadLevel("Dungeon Level")),
            ("Cave", () => DebugActions.LoadLevel("Cave Level")),
            ("Hell", () => DebugActions.LoadLevel("Hell Level")),
            ("Tutorial", () => DebugActions.LoadLevel("Tutorial")),
        });

        BuildSection("Player / teleport", new List<(string, UnityEngine.Events.UnityAction)>
        {
            ("Full heal", DebugActions.FullHeal),
            ("Kill player", DebugActions.KillPlayer),
            ("+25 EXP", () => DebugActions.AddExperience(25f)),
            ("-25 EXP", () => DebugActions.AddExperience(-25f)),
            ("Boss room", DebugActions.TeleportToBossRoom),
            ("Next spot", DebugActions.TeleportNextSpot),
            ("Test skill gem", ForceEquipTestGems),
            ("Enemy outline", LastEnemiesHint.DebugToggle),
        });

        // One button per gauntlet the game knows about.
        var gauntlets = new List<(string, UnityEngine.Events.UnityAction)>();
        foreach (GameObject prefab in gauntletPrefabs)
        {
            if (prefab == null) continue;
            GameObject captured = prefab;
            gauntlets.Add((prefab.name.Replace("Gauntlet", "").Replace("(Clone)", "").Trim(), () => SpawnGauntlet(captured)));
        }
        if (gauntlets.Count > 0) BuildSection("Spawn gauntlet", gauntlets);

        rootGo.SetActive(false);
    }

    private void BuildHeader()
    {
        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(_root, false);
        header.GetComponent<LayoutElement>().minHeight = 28f;

        var row = header.GetComponent<HorizontalLayoutGroup>();
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        TMP_Text title = MakeText(header.transform, "DEBUG  (press 0)", 20, TextAlignmentOptions.MidlineLeft);
        title.color = new Color(1f, 0.85f, 0.4f);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Button toggle = MakeButton(header.transform, "-", 28f, () => Apply(_state == State.Open ? State.Minimised : State.Open));
        toggle.GetComponent<LayoutElement>().minWidth = 28f;
        _toggleLabel = toggle.GetComponentInChildren<TMP_Text>();
    }

    private void BuildSection(string title, List<(string label, UnityEngine.Events.UnityAction action)> buttons)
    {
        TMP_Text heading = MakeText(_body, title, 16, TextAlignmentOptions.MidlineLeft);
        heading.color = new Color(0.7f, 0.75f, 0.85f);
        heading.gameObject.AddComponent<LayoutElement>().minHeight = 20f;

        var grid = new GameObject(title + " Buttons", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(_body, false);
        var g = grid.GetComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(138f, 28f);
        g.spacing = new Vector2(4f, 4f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;

        foreach (var (label, action) in buttons) MakeButton(grid.transform, label, 28f, action);
    }

    private TMP_Text MakeText(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private Button MakeButton(Transform parent, string label, float height, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = height;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.22f, 0.22f, 0.3f, 1f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.5f);
        colors.pressedColor = new Color(0.7f, 0.6f, 0.35f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(action);

        TMP_Text text = MakeText(go.transform, label, 15, TextAlignmentOptions.Center);
        var rt = (RectTransform)text.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4f, 0f);
        rt.offsetMax = new Vector2(-4f, 0f);
        text.color = new Color(0.95f, 0.95f, 0.95f);
        return button;
    }

    // Anything that depends on live state (nothing yet): kept as a hook for sections that need it.
    private void Refresh() { }
    #endregion

    #region Actions that need this component's references
    public void SpawnGauntlet(GameObject gauntletPrefab)
    {
        if (inventoryManager == null) inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogWarning("[DebugMenu] No InventoryManager in this scene.");
            return;
        }

        // Roll a random rarity (0 = Normal, 1 = Rare, 2 = SuperRare); the integer max is exclusive.
        GauntletRarity rarity = (GauntletRarity)Random.Range(0, 3);
        Debug.Log($"[DebugMenu] Spawning {gauntletPrefab.name} with rarity {rarity}");

        Apply(State.Hidden); // get out of the way: the inventory takes over the screen
        inventoryManager.TryEquipNewGauntlet(gauntletPrefab, rarity);
    }

    private void ForceEquipTestGems()
    {
        if (statsManager == null) statsManager = FindFirstObjectByType<PlayerStatsManager>();
        if (statsManager == null || GenericTestGem == null)
        {
            Debug.LogWarning("[DebugMenu] No PlayerStatsManager or test gem.");
            return;
        }

        if (statsManager.PrimaryGauntlet != null) statsManager.PrimaryGauntlet.ActiveSkillGem = GenericTestGem;
        if (statsManager.SecondaryGauntlet != null) statsManager.SecondaryGauntlet.ActiveSkillGem = GenericTestGem;
        Debug.Log($"[DebugMenu] {GenericTestGem.name} slotted into both gauntlets.");
    }
    #endregion
}

/// <summary>The cheat actions, as plain functions the debug menu's buttons call.</summary>
public static class DebugActions
{
    public static void NextLevel()
    {
        string next = SceneManager.GetActiveScene().name switch
        {
            "Dungeon Level" => "Cave Level",
            "Cave Level" => "Hell Level",
            _ => null
        };

        // Scenes can still name a next level themselves (the old DevCheats field).
        if (next == null)
        {
            DevCheats cheats = Object.FindFirstObjectByType<DevCheats>();
            if (cheats != null && !string.IsNullOrEmpty(cheats.nextLevelName)) next = cheats.nextLevelName;
        }

        if (next == null)
        {
            Debug.LogWarning("[DebugMenu] No next level known for this scene.");
            return;
        }

        LoadLevel(next);
    }

    public static void LoadLevel(string sceneName)
    {
        // Pack the backpack so the gems carry over, unless this is the tutorial (which keeps nothing).
        if (SceneManager.GetActiveScene().name != "Tutorial" && PersistentEquipment.Instance != null && InventoryManager.Instance != null)
        {
            PersistentEquipment.Instance.SaveEquipment(InventoryManager.Instance);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public static void ReloadScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static void KillPlayer()
    {
        PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null && !health.IsDead) health.TakeDamage(9999f);
    }

    public static void FullHeal()
    {
        PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null && !health.IsDead) health.Heal(health.MaxHealth);
    }

    public static void AddExperience(float amount)
    {
        ProgressBarCircle bar = ProgressBarCircle.Instance;
        if (bar == null) return;

        if (amount >= 0f) bar.AddExperience(amount);
        else bar.RemoveExperience(-amount);
    }

    public static void TeleportToBossRoom()
    {
        PlayerManager player = Object.FindFirstObjectByType<PlayerManager>();
        if (player != null) player.DebugTeleport();
    }

    public static void TeleportNextSpot()
    {
        DevCheats cheats = Object.FindFirstObjectByType<DevCheats>();
        if (cheats != null) cheats.TeleportPlayer();
        else Debug.LogWarning("[DebugMenu] This scene has no teleport spots (no DevCheats).");
    }
}

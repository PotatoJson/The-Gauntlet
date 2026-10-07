using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

/// <summary>
/// A shrine the player can use once. Walk up, press the Interact key (E), pick one gem to upgrade a tier.
/// Spent shrines stay spent through level loads and save files (tracked by PersistentEquipment).
/// </summary>
public class ShrineInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("The world-space prompt prefab (UI/InteractionPrompt).")]
    [SerializeField] private GameObject promptPrefab;
    [SerializeField] private float interactionRadius = 4.5f;
    [SerializeField] private float promptHeight = 4.4f;

    [Header("Text (Gem string table)")]
    [SerializeField] private LocalizedString promptString = new LocalizedString("Gem", "Shrine_Prompt");
    [SerializeField] private LocalizedString spentString = new LocalizedString("Gem", "Shrine_Spent");

    [Header("Spent look")]
    [Tooltip("Colour multiplier applied to the shrine once its power is used.")]
    [SerializeField] private Color spentTint = new Color(0.45f, 0.45f, 0.5f, 1f);

    private InputAction _interactAction;
    private Transform _player;
    private GameObject _prompt;
    private TMP_Text _promptText;
    private bool _inRange;
    private bool _spent;

    /// <summary>Stable per-shrine id: same shrine, same key, every time the level loads.</summary>
    public string Key
    {
        get
        {
            Vector3 p = transform.position;
            return $"{SceneManager.GetActiveScene().name}/{name}/{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.y)},{Mathf.RoundToInt(p.z)}";
        }
    }

    private void Start()
    {
        _interactAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Interact") : null;

        if (promptPrefab != null)
        {
            _prompt = Instantiate(promptPrefab);
            _promptText = _prompt.GetComponentInChildren<TMP_Text>();
            _prompt.SetActive(false);
        }

        if (PersistentEquipment.Instance != null && PersistentEquipment.Instance.IsShrineUsed(Key)) SetSpent();
    }

    private void OnDestroy()
    {
        if (_prompt != null) Destroy(_prompt);
    }

    private void Update()
    {
        if (_player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            _player = p.transform;
        }

        bool inRange = Vector3.Distance(transform.position, _player.position) <= interactionRadius && !ShrineUpgradeUI.IsOpen;
        if (inRange != _inRange)
        {
            _inRange = inRange;
            if (_prompt != null) _prompt.SetActive(inRange);
        }

        if (!_inRange) return;

        if (_prompt != null)
        {
            _prompt.transform.position = transform.position + Vector3.up * promptHeight;
            if (_promptText != null) _promptText.text = _spent ? spentString.GetLocalizedString() : BuildPrompt();
        }

        if (!_spent && Time.timeScale > 0f && _interactAction != null && _interactAction.WasPressedThisFrame())
        {
            ShrineUpgradeUI.Open(this);
        }
    }

    private string BuildPrompt()
    {
        // Show the key for whichever device the player is actually using.
        bool gamepad = Gamepad.current != null && (Keyboard.current == null || Gamepad.current.lastUpdateTime > Keyboard.current.lastUpdateTime);
        string key = "E";

        if (_interactAction != null)
        {
            for (int i = 0; i < _interactAction.bindings.Count; i++)
            {
                InputBinding b = _interactAction.bindings[i];
                if (b.isComposite) continue;

                bool isPad = b.effectivePath.Contains("<Gamepad>");
                bool isKey = b.effectivePath.Contains("<Keyboard>") || b.effectivePath.Contains("<Mouse>");
                if ((gamepad && isPad) || (!gamepad && isKey))
                {
                    key = _interactAction.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                    break;
                }
            }
        }

        promptString.Arguments = new object[] { key };
        return promptString.GetLocalizedString();
    }

    /// <summary>Called by the upgrade screen once the player has used the shrine.</summary>
    public void CompleteUse()
    {
        if (PersistentEquipment.Instance != null) PersistentEquipment.Instance.MarkShrineUsed(Key);
        SetSpent();
    }

    private void SetSpent()
    {
        _spent = true;

        // Grey the shrine out with a property block, so the shared material isn't touched.
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", spentTint);
            block.SetColor("_Color", spentTint);
            r.SetPropertyBlock(block);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}

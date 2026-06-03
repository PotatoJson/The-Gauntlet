using UnityEngine;
using UnityEngine.InputSystem;

public class GauntletInteractable : MonoBehaviour
{
    [Header("Equipment Settings")]
    [Tooltip("The gauntlet prefab to equip when interacting.")]
    public GameObject gauntletPrefab;
    [Tooltip("The rarity of the gauntlet to equip.")]
    public GauntletRarity rarity = GauntletRarity.Common;

    [Header("Interaction Settings")]
    [Tooltip("The UI object that appears when the player is in range.")]
    public GameObject interactionPrompt;
    [Tooltip("Radius within which the player can interact.")]
    public float interactionRadius = 2.5f;
    [Tooltip("Vertical offset for the prompt.")]
    public float promptVerticalOffset = 1.5f;

    private InputAction _interactAction;
    private bool _isPlayerInRange = false;
    private Transform _playerTransform;
    private Transform _promptTransform;

    private void Awake()
    {
        _interactAction = InputSystem.actions.FindAction("Interact");
    }

    private void Start()
    {
        if (interactionPrompt != null)
        {
            _promptTransform = interactionPrompt.transform;
            
            // --- SCALE FIX ---
            // If the parent (this object) has non-uniform scale, the UI will look weird.
            // We unparent the prompt so it uses global uniform scale, and we follow the object in Update.
            _promptTransform.SetParent(null);
            
            // Ensure the prompt has a reasonable uniform scale (Reset to prefab defaults or 1,1,1)
            // Note: My prefab used 0.005f for world space.
            _promptTransform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
            
            interactionPrompt.SetActive(false);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _playerTransform = player.transform;
    }

    private void Update()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            else return;
        }

        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        bool inRange = distance <= interactionRadius;

        if (inRange != _isPlayerInRange)
        {
            _isPlayerInRange = inRange;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(_isPlayerInRange);
            }
        }

        // Sync prompt position (since it's unparented)
        if (interactionPrompt != null && _isPlayerInRange)
        {
            interactionPrompt.transform.position = transform.position + Vector3.up * promptVerticalOffset;
        }

        if (_isPlayerInRange && _interactAction != null && _interactAction.WasPressedThisFrame())
        {
            PerformEquip();
        }
    }

    private void PerformEquip()
    {
        if (InventoryManager.Instance != null && gauntletPrefab != null)
        {
            bool success = InventoryManager.Instance.TryEquipNewGauntlet(gauntletPrefab, rarity);
            if (success)
            {
                if (interactionPrompt != null) Destroy(interactionPrompt);
                gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        // Cleanup unparented prompt if this object is destroyed
        if (interactionPrompt != null) Destroy(interactionPrompt);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}


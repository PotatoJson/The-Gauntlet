using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class GauntletInteractable : MonoBehaviour
{
    [Header("Loot Pool")]
    [Tooltip("Drag all possible Gauntlet Prefabs here.")]
    public List<GameObject> PossibleGauntlets;

    [Header("Level Scaling")]
    [Tooltip("Which level is this pedestal placed in? (1, 2, or 3)")]
    [Range(1, 3)]
    public int PedestalLevel = 1;

    [Header("Interaction Settings")]
    public GameObject InteractionPrompt;
    public float InteractionRadius = 2.5f;
    public float PromptVerticalOffset = 1.5f;

    // Hidden variables determined at runtime
    [HideInInspector] public GameObject GauntletPrefabA;
    [HideInInspector] public GauntletRarity RarityA;
    [HideInInspector] public GameObject GauntletPrefabB;
    [HideInInspector] public GauntletRarity RarityB;

    private InputAction _interactAction;
    private bool _isPlayerInRange = false;
    private Transform _playerTransform;
    private Transform _promptTransform;

    private void Awake()
    {
        _interactAction = InputSystem.actions.FindAction("Interact");
        GenerateRandomLoot();
    }

    private void Start()
    {
        if (InteractionPrompt != null)
        {
            _promptTransform = InteractionPrompt.transform;
            _promptTransform.SetParent(null);
            _promptTransform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
            InteractionPrompt.SetActive(false);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _playerTransform = player.transform;
    }

    private void GenerateRandomLoot()
    {
        if (PossibleGauntlets == null || PossibleGauntlets.Count < 2)
        {
            Debug.LogError("GauntletInteractable needs at least 2 gauntlets in its PossibleGauntlets list!");
            return;
        }

        // 1. Pick Option A
        int indexA = Random.Range(0, PossibleGauntlets.Count);
        GauntletPrefabA = PossibleGauntlets[indexA];
        RarityA = GetRandomRarity(PedestalLevel);

        // 2. Pick Option B (Ensure it's not the same element as A)
        int indexB = Random.Range(0, PossibleGauntlets.Count);
        while (indexB == indexA)
        {
            indexB = Random.Range(0, PossibleGauntlets.Count);
        }
        GauntletPrefabB = PossibleGauntlets[indexB];
        RarityB = GetRandomRarity(PedestalLevel);
    }

    private GauntletRarity GetRandomRarity(int level)
    {
        float roll = Random.value;

        switch (level)
        {
            case 1:
                // 70% Common, 30% Rare
                if (roll <= 0.70f) return GauntletRarity.Common;
                return GauntletRarity.Rare;
            case 2:
                // 40% Common, 40% Rare, 20% UltraRare
                if (roll <= 0.40f) return GauntletRarity.Common;
                if (roll <= 0.80f) return GauntletRarity.Rare; 
                return GauntletRarity.UltraRare;
            case 3:
                // 10% Common, 50% Rare, 40% UltraRare
                if (roll <= 0.10f) return GauntletRarity.Common;
                if (roll <= 0.60f) return GauntletRarity.Rare; 
                return GauntletRarity.UltraRare;
            default:
                return GauntletRarity.Common;
        }
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
        bool inRange = distance <= InteractionRadius;

        if (inRange != _isPlayerInRange)
        {
            _isPlayerInRange = inRange;
            if (InteractionPrompt != null)
            {
                InteractionPrompt.SetActive(_isPlayerInRange);
            }
        }

        if (InteractionPrompt != null && _isPlayerInRange)
        {
            InteractionPrompt.transform.position = transform.position + Vector3.up * PromptVerticalOffset;
        }

        if (_isPlayerInRange && _interactAction != null && _interactAction.WasPressedThisFrame())
        {
            OpenChoiceMenu();
        }
    }

    private void OpenChoiceMenu()
    {
        if (GauntletChoiceUI.Instance != null)
        {
            // We pass this script directly into the UI so the UI can read its data
            GauntletChoiceUI.Instance.OpenChoiceMenu(this);
        }
        else
        {
            Debug.LogWarning("GauntletChoiceUI is missing from the scene!");
        }
    }

    public void CompleteInteraction()
    {
        if (InteractionPrompt != null) Destroy(InteractionPrompt);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (InteractionPrompt != null) Destroy(InteractionPrompt);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractionRadius);
    }
}
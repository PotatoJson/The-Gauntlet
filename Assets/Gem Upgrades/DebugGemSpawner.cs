using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // For your Gem Text
using System.Collections.Generic; // For Stacks

public class DebugGemSpawner : MonoBehaviour
{
    [Header("Gem Prefabs")]
    [SerializeField] private GameObject coreGemPrefab;
    [SerializeField] private GameObject techniqueGemPrefab;
    [SerializeField] private GameObject augmentGemPrefab;

    [Header("Grid Containers (Content Objects)")]
    [SerializeField] private Transform coreGridContainer;
    [SerializeField] private Transform techniqueGridContainer;
    [SerializeField] private Transform augmentGridContainer;

    // Trackers for the numbers
    private int _coreCounter = 1;
    private int _techCounter = 1;
    private int _augCounter = 1;

    // Memory stacks so we know which ones to delete
    private Stack<GameObject> _spawnedCoreGems = new Stack<GameObject>();
    private Stack<GameObject> _spawnedTechGems = new Stack<GameObject>();
    private Stack<GameObject> _spawnedAugGems = new Stack<GameObject>();

    private void Update()
    {
        if (Keyboard.current == null) return;

        // --- SPAWN GEMS (1, 2, 3) ---
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SpawnGem(coreGemPrefab, coreGridContainer, ref _coreCounter, _spawnedCoreGems);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SpawnGem(techniqueGemPrefab, techniqueGridContainer, ref _techCounter, _spawnedTechGems);
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SpawnGem(augmentGemPrefab, augmentGridContainer, ref _augCounter, _spawnedAugGems);

        // --- DELETE GEMS (8, 9, 0) ---
        if (Keyboard.current.digit8Key.wasPressedThisFrame)
            DeleteLastGem(_spawnedCoreGems, ref _coreCounter);
        if (Keyboard.current.digit9Key.wasPressedThisFrame)
            DeleteLastGem(_spawnedTechGems, ref _techCounter);
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
            DeleteLastGem(_spawnedAugGems, ref _augCounter);
    }

    private void SpawnGem(GameObject prefab, Transform container, ref int counter, Stack<GameObject> memoryStack)
    {
        if (prefab != null && container != null)
        {
            GameObject newGem = Instantiate(prefab, container);

            // Find the text component and update it with the current number
            TMP_Text gemText = newGem.GetComponentInChildren<TMP_Text>();
            if (gemText != null)
            {
                gemText.text = counter.ToString();
            }

            // Increase the number for the next time, and remember this gem
            counter++;
            memoryStack.Push(newGem);
        }
    }

    private void DeleteLastGem(Stack<GameObject> memoryStack, ref int counter)
    {
        // Check if we actually have gems to delete
        if (memoryStack.Count > 0)
        {
            GameObject gemToDestroy = memoryStack.Pop();
            if (gemToDestroy != null)
            {
                Destroy(gemToDestroy);

                // Optional: step the counter backward so numbers don't skip!
                if (counter > 1) counter--;
            }
        }
    }
}
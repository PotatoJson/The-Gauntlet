using UnityEngine;
using System.Collections.Generic;

public class PotionUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your Player GameObject here")]
    public PlayerHealth playerHealth;

    [Tooltip("Drag your animated Potion UI Prefab here")]
    public GameObject potionPrefab;

    private List<GameObject> spawnedPotions = new List<GameObject>();

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnPotionCountChanged += UpdatePotionUI;

            // Draw the count we have right now as well as listening for changes. The event only
            // fires on a change, so a UI that enables after the count was last set - or after a
            // save file restored it - would otherwise keep showing a stale number of bottles.
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnPotionCountChanged -= UpdatePotionUI;
        }
    }

    /// <summary>Redraws the bottles from the player's current potion count.</summary>
    public void Refresh()
    {
        if (playerHealth == null) return;

        UpdatePotionUI(playerHealth.CurrentPotions, playerHealth.MaxPotions);
    }

    private void UpdatePotionUI(int currentCount, int maxCount)
    {
        // 1. If we have fewer potion icons than we should, spawn more!
        while (spawnedPotions.Count < currentCount)
        {
            // Instantiate creates a new prefab and automatically makes it a child of this Layout Group!
            GameObject newPotion = Instantiate(potionPrefab, transform);
            spawnedPotions.Add(newPotion);
        }

        // 2. If we have more potion icons than we should (because we drank one), destroy the last one!
        while (spawnedPotions.Count > currentCount)
        {
            GameObject potionToRemove = spawnedPotions[spawnedPotions.Count - 1];
            spawnedPotions.RemoveAt(spawnedPotions.Count - 1);
            Destroy(potionToRemove);
        }
    }
}
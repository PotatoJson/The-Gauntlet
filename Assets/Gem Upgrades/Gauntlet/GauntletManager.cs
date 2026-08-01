using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GauntletManager : MonoBehaviour
{
    [Header("Back End Data")]
    public GauntletData LinkedGauntletData;

    [Header("Gauntlet Settings")]
    [Tooltip("The base max number of slots this gauntlet naturally has (e.g., 3, 4, 5)")]
    public int baseCapacity = 5;

    [Tooltip("Drag the child 'Slot' objects here IN ORDER (Slot 1, Slot 2, etc.)")]
    public List<GameObject> fingerSlots;

    [Tooltip("Drag the child 'Ultimate Slot' object here")]
    public GameObject SkillSlot;

    [Header("UI References")]
    public Sprite defaultSlotSprite;
    public Sprite crossSprite;
    public GameObject selectionBracket;
    public float selectionBracketScale = 1.4f;

    public int currentActiveSlots { get; private set; }
    public GauntletRarity currentRarity { get; private set; }

    private Transform _currentBracketTarget;

    // Called by InventoryManager the exact moment it spawns!
    public void InitializeGauntlet(bool isPrimary, GauntletRarity rarity)
    {
        currentRarity = rarity;

        // Convert the enum to a simple math bonus (Normal = 0, Rare = 1, SuperRare = 2)
        int rarityBonus = (int)rarity;

        // Apply your exact scaling rules!
        if (isPrimary)
        {
            currentActiveSlots = Mathf.Clamp(3 + rarityBonus, 0, fingerSlots.Count); // Scales: 3, 4, 5
        }
        else
        {
            currentActiveSlots = Mathf.Clamp(1 + rarityBonus, 0, 3); // Scales: 1, 2, 3 (Hard clamp for secondary)
        }

        Debug.Log($"[GauntletManager] Initializing {gameObject.name} (Primary: {isPrimary}, Rarity: {rarity}, ActiveSlots: {currentActiveSlots})");

        if (selectionBracket != null)
        {
            selectionBracket.SetActive(false);
            UnityEngine.UI.Image[] images = selectionBracket.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach(var img in images) img.raycastTarget = false;
        }

        // Physically turn the slots on or off (seal them)
        for (int i = 0; i < fingerSlots.Count; i++)
        {
            if (fingerSlots[i] == null) continue;

            GemDropSlot slot = fingerSlots[i].GetComponent<GemDropSlot>();
            if (slot != null)
            {
                slot.Setup(this);
                
                if (i < currentActiveSlots)
                {
                    fingerSlots[i].SetActive(true);
                    // Use a circular fallback for CircularCircular
                    slot.SetSlotEnabled(defaultSlotSprite);
                }
                else
                {
                    fingerSlots[i].SetActive(true); // Keep active to show the cross
                    slot.SetSlotDisabled(crossSprite);
                }
            }
        }

        if (SkillSlot != null)
        {
            SkillSlotManager skillManager = SkillSlot.GetComponent<SkillSlotManager>();
            if (skillManager != null)
            {
                skillManager.Setup(this);
            }
            
            Button skillBtn = SkillSlot.GetComponent<Button>();
            if (skillBtn != null) skillBtn.interactable = true;
        }
    }

    public void UpdateBracket(Transform target)
    {
        if (selectionBracket == null) return;
        _currentBracketTarget = target;
        selectionBracket.transform.position = target.position;
        selectionBracket.transform.localScale = Vector3.one * selectionBracketScale;
        if (!selectionBracket.activeSelf) selectionBracket.SetActive(true);
    }

    /// <summary>
    /// Hides the shared bracket whatever it is currently pointing at. Used when input switches
    /// device, where the slot that showed it never receives a matching exit or deselect event.
    /// </summary>
    public void HideBracket()
    {
        _currentBracketTarget = null;
        if (selectionBracket != null) selectionBracket.SetActive(false);
    }

    public void ClearBracket(Transform target)
    {
        if (_currentBracketTarget == target)
        {
            _currentBracketTarget = null;
            if (selectionBracket != null) selectionBracket.SetActive(false);
        }
    }
}
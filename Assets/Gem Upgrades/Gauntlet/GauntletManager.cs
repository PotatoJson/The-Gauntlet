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
            currentActiveSlots = 3 + rarityBonus; // Scales: 3, 4, 5
        }
        else
        {
            currentActiveSlots = 1 + rarityBonus; // Scales: 1, 2, 3
        }

        if (selectionBracket != null)
        {
            selectionBracket.SetActive(false);
            // Disable animator if it's causing flickering (common with 'Image' animator controllers)
            //Animator anim = selectionBracket.GetComponent<Animator>();
            //if (anim != null) anim.enabled = false;
            
            // Ensure raycast target is off for all parts of the bracket
            UnityEngine.UI.Image[] images = selectionBracket.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach(var img in images) img.raycastTarget = false;
        }

        // Physically turn the slots on or off (seal them)
        for (int i = 0; i < fingerSlots.Count; i++)
        {
            GemDropSlot slot = fingerSlots[i].GetComponent<GemDropSlot>();
            if (slot != null)
            {
                slot.Setup(this);
                
                if (i < currentActiveSlots)
                {
                    fingerSlots[i].SetActive(true);
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

    public void ClearBracket(Transform target)
    {
        if (_currentBracketTarget == target)
        {
            _currentBracketTarget = null;
            if (selectionBracket != null) selectionBracket.SetActive(false);
        }
    }
}
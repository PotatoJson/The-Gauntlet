using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillSlotManager : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Ultimate Slot Configuration")]
    public DraggableGem CurrentSkillGem;

    private GauntletManager _manager;

    private void Awake()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.None;
        }
    }

    public void Setup(GauntletManager manager)
    {
        _manager = manager;
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject == null) return;

        DraggableGem incomingGem = droppedObject.GetComponent<DraggableGem>();

        if (incomingGem != null)
        {
            // 1. STRICT CHECK: Is this a Skill Gem?
            if (incomingGem.LinkedGemData.gemType == GemType.Skill)
            {
                // 2. REWARD LOCK CHECK: Are we allowed to pick this up right now?
                if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
                {
                    if (!RewardMenuManager.Instance.CanDragGem(incomingGem)) return;
                }

                // 3. CLAIM THE GEM: Tell the drag system this slot is its new home!
                incomingGem.parentAfterDrag = transform;
                SlotSkillGem(incomingGem);

                // 4. NOTIFY REWARD SYSTEM
                if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
                {
                    RewardMenuManager.Instance.OnGemSlotted(incomingGem);
                }
            }
            else
            {
                Debug.Log("Invalid Gem: Only Skill Gems can be equipped in the Ultimate slot.");
            }
        }
    }

    /// <summary>
    /// The skill gem actually sitting in this slot right now.
    ///
    /// Use this instead of reading CurrentSkillGem directly. SlotSkillGem assigns that field but
    /// nothing clears it when the player drags a gem back out, so it can point at a gem that has
    /// since moved elsewhere. The hierarchy is the real source of truth, and checking it here also
    /// repairs the stale field as a side effect.
    /// </summary>
    public DraggableGem GetEquippedSkillGem()
    {
        DraggableGem gem = GetComponentInChildren<DraggableGem>(true);

        // Only count a gem that is genuinely parented to this slot.
        if (gem == null || gem.transform.parent != transform)
        {
            CurrentSkillGem = null;
            return null;
        }

        CurrentSkillGem = gem;
        return gem;
    }

    public void SlotSkillGem(DraggableGem gem)
    {
        // Clear old gem if one exists
        if (CurrentSkillGem != null && CurrentSkillGem != gem)
        {
            Destroy(CurrentSkillGem.gameObject);
        }

        CurrentSkillGem = gem;

        gem.transform.SetParent(transform, false);
        gem.transform.localPosition = Vector3.zero;
        gem.transform.localScale = Vector3.one;

        // Size the gem to match the ultimate slot perfectly
        RectTransform slotRect = GetComponent<RectTransform>();
        RectTransform gemRect = gem.GetComponent<RectTransform>();
        if (slotRect != null && gemRect != null)
        {
            gemRect.sizeDelta = slotRect.rect.size;
        }

        // The gem draws its own bracket now. Any bracket this slot raised while it was empty
        // (during the drag, for instance) would otherwise stay up as a second indicator.
        HideBracket();
    }

    // Same split as GemDropSlot: hover owns the bracket on mouse, selection owns it on a controller.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!InputHelper.IsGamepadLastUsed()) ShowBracket();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!InputHelper.IsGamepadLastUsed()) HideBracket();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (InputHelper.IsGamepadLastUsed()) ShowBracket();
    }

    public void OnDeselect(BaseEventData eventData) => HideBracket();

    private void ShowBracket()
    {
        if (_manager == null) return;

        // Same reason as GemDropSlot: an equipped skill gem already shows its own bracket, and
        // pointer-enter reaches this slot too, so drawing both stacks two highlights.
        //
        // Placement mode is the exception: the gem being placed is detached, so this bracket is
        // the player's only cursor and has to show even when the slot is already filled.
        bool isPlacing = GemPopupMenu.Instance != null && GemPopupMenu.Instance.IsPlacingMode;
        if (GetEquippedSkillGem() != null && !isPlacing) return;

        _manager.UpdateBracket(transform);
    }

    private void HideBracket()
    {
        if (_manager != null)
        {
            _manager.ClearBracket(transform);
        }
    }
}
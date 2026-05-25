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
    }

    public void OnPointerEnter(PointerEventData eventData) => ShowBracket();
    public void OnPointerExit(PointerEventData eventData) => HideBracket();
    public void OnSelect(BaseEventData eventData) => ShowBracket();
    public void OnDeselect(BaseEventData eventData) => HideBracket();

    private void ShowBracket()
    {
        if (_manager == null) return;
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
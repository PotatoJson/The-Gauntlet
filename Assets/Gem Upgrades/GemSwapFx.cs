using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swapping two equipped gems, made easy to read: the two gems fly past each other on opposite arcs (one over,
/// one under), growing a little mid-air and drawn above everything. When each lands its slot gives a gold pulse
/// and the gem pops, with a small "tock-tick" sound. The gems are moved into their new slots immediately, so the
/// game state is correct from the first frame; only the way they travel there is animated.
/// </summary>
public static class GemSwapFx
{
    private const float FlightTime = 0.42f;
    private static AudioSource _audio;

    /// <summary>True for an actual gauntlet socket, as opposed to the reward board or the grid.</summary>
    public static bool IsGauntletSlot(Transform t)
    {
        return t != null && (t.GetComponent<GemDropSlot>() != null || t.GetComponent<SkillSlotManager>() != null);
    }

    /// <summary>
    /// Swaps <paramref name="a"/> into <paramref name="slotA"/> and <paramref name="b"/> into <paramref name="slotB"/>.
    /// Either gem may be wherever it currently is (in a slot, or held at the cursor).
    /// </summary>
    public static void Swap(DraggableGem a, Transform slotA, DraggableGem b, Transform slotB)
    {
        if (a == null || b == null || slotA == null || slotB == null) return;

        Vector3 startA = a.transform.position;
        Vector3 startB = b.transform.position;

        Place(a, slotA);
        Place(b, slotB);

        Fly(a, startA, slotA, +1f);
        Fly(b, startB, slotB, -1f);

        PlaySound();

        PlayerStatsManager stats = Object.FindFirstObjectByType<PlayerStatsManager>();
        if (stats != null && InventoryManager.Instance != null)
        {
            stats.SyncWithUI(InventoryManager.Instance.primaryGauntlet, InventoryManager.Instance.secondaryGauntlet);
        }
    }

    private static void Place(DraggableGem gem, Transform slot)
    {
        gem.parentAfterDrag = slot;
        gem.transform.SetParent(slot, true); // keeps its world position; the flight animates from there

        RectTransform gemRect = gem.GetComponent<RectTransform>();
        RectTransform slotRect = slot.GetComponent<RectTransform>();
        if (gemRect != null && slotRect != null) gemRect.sizeDelta = slotRect.rect.size;
    }

    private static void Fly(DraggableGem gem, Vector3 start, Transform slot, float side)
    {
        Vector3 end = slot.position;
        Vector3 dir = end - start;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        perp = perp.sqrMagnitude > 0.0001f ? perp.normalized : Vector3.up;
        float height = Mathf.Max(70f, dir.magnitude * 0.45f) * side;

        // Draw above everything while travelling, and ignore the pointer so it can't be grabbed mid-flight.
        Canvas canvas = gem.GetComponent<Canvas>();
        bool addedCanvas = canvas == null;
        if (addedCanvas) canvas = gem.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;

        CanvasGroup group = gem.GetComponent<CanvasGroup>();
        if (group != null) group.blocksRaycasts = false;

        gem.transform.DOKill();
        gem.transform.position = start;

        DOVirtual.Float(0f, 1f, FlightTime, t =>
            {
                if (gem == null) return;
                gem.transform.position = Vector3.LerpUnclamped(start, end, t) + perp * (height * Mathf.Sin(Mathf.PI * t));
                gem.transform.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(Mathf.PI * t));
            })
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .SetLink(gem.gameObject)
            .OnComplete(() =>
            {
                if (gem == null) return;
                gem.transform.localPosition = Vector3.zero;
                gem.transform.localScale = Vector3.one;
                if (addedCanvas) Object.Destroy(canvas);
                if (group != null) group.blocksRaycasts = true;

                PulseAt(slot, new Color(1f, 0.85f, 0.35f, 1f));
                gem.PlayLandFeedback();
            });
    }

    /// <summary>A ring that swells out of a slot and fades: "something landed here".</summary>
    public static void PulseAt(Transform slot, Color color)
    {
        RectTransform slotRect = slot as RectTransform;
        if (slotRect == null || slot.parent == null) return;

        var go = new GameObject("SlotPulse", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = slot.gameObject.layer;

        // Parented beside the slot, not in it, so the slot's own logic never sees an extra child.
        var rt = (RectTransform)go.transform;
        rt.SetParent(slot.parent, false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = slot.position;
        float size = Mathf.Max(slotRect.rect.width, slotRect.rect.height) * 1.6f;
        rt.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();
        img.sprite = Resources.Load<Sprite>("GemFx/aura_halo");
        img.material = Resources.Load<Material>("GemFx/AuraAdditive");
        img.raycastTarget = false;
        img.color = color;

        rt.localScale = Vector3.one * 0.6f;
        rt.DOScale(1.5f, 0.38f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(go);
        img.DOFade(0f, 0.38f).SetEase(Ease.InQuad).SetUpdate(true).SetLink(go).OnComplete(() => Object.Destroy(go));
    }

    public static void PlaySound()
    {
        if (_audio == null)
        {
            GameObject prefab = Resources.Load<GameObject>("GemFx/GemFxAudio");
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab);
            Object.DontDestroyOnLoad(go);
            _audio = go.GetComponent<AudioSource>();
        }

        if (_audio != null) _audio.Play();
    }
}

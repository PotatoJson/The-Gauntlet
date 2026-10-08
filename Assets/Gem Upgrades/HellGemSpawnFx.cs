using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The reward menu's gems arrive "Hellbent" style, one at a time. Where a gem will sit, a summoning circle
/// flickers up under a ring of hellfire that flares outward, then PULLS BACK in on itself, dragging embers with
/// it. As the ring collapses the gem is born out of the centre in a flash of fire. Then the next gem.
///
/// Everything is UI (additive images under the gems) and runs on unscaled time, because the menu is paused.
/// </summary>
public static class HellGemSpawnFx
{
    private const float Stagger = 0.65f;     // time between one gem starting and the next
    private const string Folder = "GemFx/";
    private const string TimerId = "HellGemSpawnTimer";

    private static readonly Color Fire = new Color(1f, 0.55f, 0.15f, 1f);
    private static readonly Color Hot = new Color(1f, 0.92f, 0.65f, 1f);
    private static readonly Color Blood = new Color(0.9f, 0.18f, 0.08f, 1f);

    private static AudioSource _audio;
    private static Sprite _ring, _runes, _ember, _glow;
    private static Material _add;

    /// <summary>Hides every gem, then brings them in one after another.</summary>
    public static void Play(List<GameObject> gems, Transform container, RectTransform shakeTarget)
    {
        LoadAssets();

        // A new sequence replaces any one still running (e.g. the menu was reopened), so no stray effects are left over.
        DOTween.Kill(TimerId);

        foreach (GameObject gem in gems) Hide(gem);

        for (int i = 0; i < gems.Count; i++)
        {
            GameObject gem = gems[i];
            float delay = i * Stagger;

            // A tiny inert tween as the timer: it dies with the gem, so closing the menu mid-sequence is safe.
            int index = i;
            DOVirtual.DelayedCall(delay, () => Spawn(gem, container, shakeTarget, index == 0), ignoreTimeScale: true)
                .SetId(TimerId)
                .SetUpdate(true)
                .SetLink(gem);
        }
    }

    private static void LoadAssets()
    {
        if (_ring != null) return;
        _ring = Resources.Load<Sprite>(Folder + "hell_fire_ring");
        _runes = Resources.Load<Sprite>(Folder + "hell_runes");
        _ember = Resources.Load<Sprite>(Folder + "hell_ember");
        _glow = Resources.Load<Sprite>(Folder + "hell_glow");
        _add = Resources.Load<Material>(Folder + "AuraAdditive");
    }

    // ------------------------------------------------------------------ gem visibility

    private static void Hide(GameObject gem)
    {
        if (gem == null) return;

        CanvasGroup group = gem.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        Button btn = gem.GetComponent<Button>();
        if (btn != null) btn.interactable = false;

        gem.transform.localScale = Vector3.zero;
    }

    private static void Reveal(GameObject gem, bool takeControllerFocus)
    {
        if (gem == null) return;

        // If the player has already claimed another reward while later gems were still arriving, this gem
        // appears dimmed and locked, exactly as the reward menu would have left it.
        DraggableGem drag = gem.GetComponent<DraggableGem>();
        bool usable = drag == null || RewardMenuManager.Instance == null || RewardMenuManager.Instance.CanDragGem(drag);

        CanvasGroup group = gem.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.blocksRaycasts = true;
            DOTween.To(() => group.alpha, a => group.alpha = a, usable ? 1f : 0.4f, 0.2f).SetUpdate(true).SetLink(gem);
        }

        Button btn = gem.GetComponent<Button>();
        if (btn != null) btn.interactable = usable;

        // The menu normally focuses the first gem for controller players, but it was hidden at that moment.
        if (takeControllerFocus && usable && UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(gem);
        }

        RectTransform rect = (RectTransform)gem.transform;
        Vector2 home = rect.anchoredPosition;
        rect.anchoredPosition = home + new Vector2(0f, -26f);
        rect.DOAnchorPos(home, 0.4f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gem);
        rect.localScale = Vector3.one * 0.15f;
        rect.DOScale(1f, 0.42f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true).SetLink(gem);

        // Born in fire: starts glowing orange and cools to its own colours.
        Image image = gem.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(1f, 0.45f, 0.15f, 1f);
            image.DOColor(Color.white, 0.55f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gem);
        }
    }

    // ------------------------------------------------------------------ one gem's summoning

    private static void Spawn(GameObject gem, Transform container, RectTransform shakeTarget, bool first)
    {
        // Skip a gem that is already on its way out (destroyed this frame but not gone yet).
        if (gem == null || container == null || !gem.activeInHierarchy || gem.transform.parent != container) return;

        RectTransform gemRect = (RectTransform)gem.transform;
        Vector2 pos = gemRect.anchoredPosition;
        int under = gem.transform.GetSiblingIndex(); // effects sit just beneath the gem

        // 1. Summoning circle: fades up under the gem, turning slowly, fades away after the birth.
        Image runes = NewImage("HellRunes", container, _runes, new Color(Blood.r, Blood.g, Blood.b, 0f), 150f, pos, under);
        runes.rectTransform.localScale = Vector3.one * 0.6f;
        runes.rectTransform.DOScale(1.05f, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(runes.gameObject);
        runes.DOFade(0.85f, 0.25f).SetUpdate(true).SetLink(runes.gameObject);
        runes.rectTransform.DORotate(new Vector3(0f, 0f, -130f), 1.6f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetUpdate(true).SetLink(runes.gameObject);
        runes.DOFade(0f, 0.45f).SetDelay(0.95f).SetUpdate(true).SetLink(runes.gameObject).OnComplete(() => Object.Destroy(runes.gameObject));

        // 2. The ring of hellfire flares OUT...
        Image ring = NewImage("HellRing", container, _ring, Color.white, 128f, pos, under + 1);
        ring.rectTransform.localScale = Vector3.one * 0.25f;
        ring.color = new Color(1f, 1f, 1f, 0f);
        Sequence ringSeq = DOTween.Sequence().SetUpdate(true).SetLink(ring.gameObject);
        ringSeq.Append(ring.rectTransform.DOScale(1.55f, 0.32f).SetEase(Ease.OutCubic));
        ringSeq.Join(ring.DOFade(1f, 0.12f));
        ringSeq.Join(ring.rectTransform.DORotate(new Vector3(0f, 0f, 60f), 0.32f, RotateMode.FastBeyond360));
        // ...hangs for a beat, then PULLS BACK in, hard.
        ringSeq.AppendInterval(0.08f);
        ringSeq.Append(ring.rectTransform.DOScale(0.1f, 0.34f).SetEase(Ease.InCubic));
        ringSeq.Join(ring.rectTransform.DORotate(new Vector3(0f, 0f, -90f), 0.34f, RotateMode.FastBeyond360));
        ringSeq.Join(ring.DOColor(new Color(1f, 0.85f, 0.5f, 1f), 0.34f));
        ringSeq.Insert(0.62f, ring.DOFade(0f, 0.12f)); // vanishes as it reaches the centre
        ringSeq.OnComplete(() => Object.Destroy(ring.gameObject));

        // Embers get dragged in with the pull-back.
        float pullStart = 0.32f + 0.08f;
        for (int i = 0; i < 12; i++)
        {
            float angle = (i / 12f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            float radius = Random.Range(88f, 120f);
            Vector2 from = pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Image ember = NewImage("Ember", container, _ember, Color.Lerp(Fire, Hot, Random.value), 12f, from, under + 2);
            ember.color = new Color(ember.color.r, ember.color.g, ember.color.b, 0f);

            float delay = pullStart + Random.Range(0f, 0.08f);
            ember.DOFade(1f, 0.05f).SetDelay(delay).SetUpdate(true).SetLink(ember.gameObject);
            ember.rectTransform.DOAnchorPos(pos, 0.3f).SetDelay(delay).SetEase(Ease.InCubic).SetUpdate(true).SetLink(ember.gameObject);
            ember.DOFade(0f, 0.08f).SetDelay(delay + 0.24f).SetUpdate(true).SetLink(ember.gameObject).OnComplete(() => Object.Destroy(ember.gameObject));
        }

        // 3. As the ring collapses, the gem is born in a burst.
        float birth = pullStart + 0.22f;
        DOVirtual.DelayedCall(birth, () =>
            {
                if (gem == null) return;
                Reveal(gem, first);
                Burst(container, pos, shakeTarget);
            }, ignoreTimeScale: true)
            .SetId(TimerId).SetUpdate(true).SetLink(gem);

        // The clip builds for half a second and ignites with a thump, so start it a little before the birth.
        DOVirtual.DelayedCall(birth - 0.5f, PlaySound, ignoreTimeScale: true).SetId(TimerId).SetUpdate(true).SetLink(gem);
    }

    private static void Burst(Transform container, Vector2 pos, RectTransform shakeTarget)
    {
        // white-hot flash
        Image flash = NewImage("HellFlash", container, _glow, Hot, 120f, pos, -1);
        flash.rectTransform.localScale = Vector3.one * 0.5f;
        flash.rectTransform.DOScale(2.1f, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(flash.gameObject);
        flash.DOFade(0f, 0.3f).SetEase(Ease.InQuad).SetUpdate(true).SetLink(flash.gameObject).OnComplete(() => Object.Destroy(flash.gameObject));

        // shockwave: a thin fire ring thrown outward
        Image wave = NewImage("HellWave", container, _ring, new Color(1f, 0.5f, 0.15f, 0.9f), 128f, pos, -1);
        wave.rectTransform.localScale = Vector3.one * 0.5f;
        wave.rectTransform.DOScale(2.2f, 0.42f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(wave.gameObject);
        wave.DOFade(0f, 0.42f).SetEase(Ease.InQuad).SetUpdate(true).SetLink(wave.gameObject).OnComplete(() => Object.Destroy(wave.gameObject));

        // embers thrown outward, rising as they cool
        for (int i = 0; i < 16; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float dist = Random.Range(60f, 150f);
            Vector2 to = pos + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist * 0.8f + Random.Range(10f, 60f));
            Image ember = NewImage("Ember", container, _ember, Color.Lerp(Fire, Hot, Random.value), Random.Range(8f, 14f), pos, -1);
            float t = Random.Range(0.4f, 0.75f);
            ember.rectTransform.DOAnchorPos(to, t).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(ember.gameObject);
            ember.DOColor(new Color(Blood.r, Blood.g, Blood.b, 0f), t).SetEase(Ease.InQuad).SetUpdate(true).SetLink(ember.gameObject)
                .OnComplete(() => Object.Destroy(ember.gameObject));
        }

        // a short, heavy shake of the whole board
        if (shakeTarget != null)
        {
            shakeTarget.DOKill(true);
            shakeTarget.DOShakeAnchorPos(0.22f, 7f, 22, 90f, false, true).SetUpdate(true).SetLink(shakeTarget.gameObject);
        }
    }

    // ------------------------------------------------------------------ helpers

    // siblingIndex < 0 = on top of everything
    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color, float size, Vector2 anchoredPos, int siblingIndex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;

        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPos;
        if (siblingIndex < 0) rt.SetAsLastSibling();
        else rt.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.material = _add;
        img.color = color;
        img.raycastTarget = false; // never in the way of the gems
        return img;
    }

    private static void PlaySound()
    {
        if (_audio == null)
        {
            GameObject prefab = Resources.Load<GameObject>(Folder + "HellSpawnAudio");
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab);
            Object.DontDestroyOnLoad(go);
            _audio = go.GetComponent<AudioSource>();
        }

        if (_audio != null) _audio.PlayOneShot(_audio.clip); // one-shot, so the three gems' sounds can overlap
    }
}

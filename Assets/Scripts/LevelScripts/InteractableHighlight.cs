using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// When a room is cleared, the gauntlet chests and shrines near it that can still be used start to glow, and a short
/// notice at the top of the screen says what's there. The glow stays until the thing is used: a chest disappears when
/// it's opened, and a shrine stops glowing once it's spent.
/// </summary>
public class InteractableHighlight : MonoBehaviour
{
    private const float Radius = 40f;          // how far from the player, at the clear, counts as "this room"
    private const float NoticeSeconds = 3.5f;
    private const string GlowMaterialPath = "Highlight/InteractableGlow";
    private static readonly Color ChestColor = new Color(1f, 0.75f, 0.3f);
    private static readonly Color ShrineColor = new Color(0.4f, 1f, 0.88f);

    private static Material _glow;
    private static NoticeBanner _banner;

    private ShrineInteractable _shrine;

    /// <summary>Lights up the usable chests and shrines around this point, and says so.</summary>
    public static void ShowNear(Vector3 point)
    {
        int chests = 0, shrines = 0;

        // An opened chest switches itself off, so every active one can still be used.
        foreach (GauntletInteractable chest in FindObjectsByType<GauntletInteractable>(FindObjectsSortMode.None))
        {
            if (!Near(chest.transform, point)) continue;
            Add(chest.gameObject, ChestColor, null);
            chests++;
        }

        foreach (ShrineInteractable shrine in FindObjectsByType<ShrineInteractable>(FindObjectsSortMode.None))
        {
            if (shrine.IsSpent || !Near(shrine.transform, point)) continue;
            Add(shrine.gameObject, ShrineColor, shrine);
            shrines++;
        }

        string key = chests > 0 && shrines > 0 ? "Notice_ChestAndShrine" : chests > 0 ? "Notice_Chest" : shrines > 0 ? "Notice_Shrine" : null;
        if (key == null) return;

        if (_banner == null) _banner = NoticeBanner.Create();
        _banner.Show(LocalizationSettings.StringDatabase.GetLocalizedString("UIStrings", key), NoticeSeconds);
    }

    private static bool Near(Transform thing, Vector3 point) => (thing.position - point).sqrMagnitude <= Radius * Radius;

    private static void Add(GameObject target, Color color, ShrineInteractable shrine)
    {
        if (target.GetComponent<InteractableHighlight>() != null) return; // already glowing

        if (_glow == null) _glow = Resources.Load<Material>(GlowMaterialPath);
        if (_glow == null) return;

        InteractableHighlight highlight = target.AddComponent<InteractableHighlight>();
        highlight._shrine = shrine;

        Material material = new Material(_glow);
        material.SetColor("_Color", color);

        // A copy of each mesh, drawn with the glow over the real one. The object's own materials stay as they are.
        foreach (MeshRenderer renderer in target.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.GetComponent<TMP_Text>() != null) continue; // world-space prompt text
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            GameObject copy = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
            copy.transform.SetParent(renderer.transform, false);
            copy.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer glowRenderer = copy.GetComponent<MeshRenderer>();
            Material[] materials = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            glowRenderer.sharedMaterials = materials;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
        }
    }

    private void Update()
    {
        if (_shrine != null && _shrine.IsSpent) Remove();
    }

    private void Remove()
    {
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.gameObject.name == "Glow") Destroy(renderer.gameObject);
        }
        Destroy(this);
    }

    /// <summary>A line of text at the top of the screen that fades in, waits and fades out.</summary>
    private class NoticeBanner : MonoBehaviour
    {
        private TMP_Text _text;
        private CanvasGroup _group;
        private Coroutine _showing;
        private LocalizedTmpFont _font;

        public static NoticeBanner Create()
        {
            GameObject host = new GameObject("[InteractableNotice]");
            DontDestroyOnLoad(host);
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31500; // over the HUD, under the story pages and the loading screen
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            NoticeBanner banner = host.AddComponent<NoticeBanner>();
            banner._group = host.AddComponent<CanvasGroup>();
            banner._group.alpha = 0f;
            banner._group.blocksRaycasts = false;

            TextMeshProUGUI text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(host.transform, false);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(1200f, 80f);
            rect.anchoredPosition = new Vector2(0f, -150f);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 34f;
            text.color = new Color(1f, 0.93f, 0.78f);
            text.raycastTarget = false;
            banner._text = text;

            banner._font = new LocalizedTmpFont { TableReference = "UIFonts", TableEntryReference = "MainFont" };
            banner._font.AssetChanged += font => { if (font != null) text.font = font; };
            return banner;
        }

        public void Show(string message, float seconds)
        {
            _text.text = $"<mark=#000000A0 padding=\"20,20,10,10\">{message}</mark>";
            if (_showing != null) StopCoroutine(_showing);
            _showing = StartCoroutine(ShowRoutine(seconds));
        }

        private IEnumerator ShowRoutine(float seconds)
        {
            for (float t = _group.alpha * 0.25f; t < 0.25f; t += Time.unscaledDeltaTime) { _group.alpha = t / 0.25f; yield return null; }
            _group.alpha = 1f;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { _group.alpha = 1f - t / 0.5f; yield return null; }
            _group.alpha = 0f;
        }
    }
}

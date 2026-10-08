using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A glowing aura around a gem, getting richer with its tier (quality):
///   Tier 2  soft halo
///   Tier 3  brighter halo + sparkles orbiting the gem
///   Tier 4  stronger halo + slowly turning light rays + more sparkles
///   Tier 5  strongest halo and rays + embers rising off the gem
/// Built from code under the gem and tinted with the tier's colour. It is all additive and non-interactive, and
/// the halo has a clear centre, so the gem itself is never covered. Runs on unscaled time (the inventory is paused).
/// </summary>
public class GemAura : MonoBehaviour
{
    private const int MaxSparks = 7;
    private const string Folder = "GemFx/";

    private struct Look
    {
        public float Halo, Pulse, Rays, RaySpeed;
        public int Sparks;
        public bool Rise;
    }

    private static Look LookFor(int tier)
    {
        switch (tier)
        {
            case 2: return new Look { Halo = 0.30f, Pulse = 2.2f };
            case 3: return new Look { Halo = 0.42f, Pulse = 2.8f, Sparks = 3 };
            case 4: return new Look { Halo = 0.55f, Pulse = 3.4f, Rays = 0.16f, RaySpeed = 25f, Sparks = 5 };
            default: return new Look { Halo = 0.70f, Pulse = 4.2f, Rays = 0.28f, RaySpeed = 40f, Sparks = 7, Rise = true };
        }
    }

    private RectTransform _selfRect, _gemRect;
    private Image _halo, _rays;
    private Image[] _sparks;
    private Look _look;
    private Color _color = Color.white;
    private float _phase;

    /// <summary>Gets the gem's aura, building it the first time.</summary>
    public static GemAura Ensure(DraggableGem gem)
    {
        Transform existing = gem.transform.Find("Aura");
        if (existing != null) return existing.GetComponent<GemAura>();

        var go = new GameObject("Aura", typeof(RectTransform));
        go.layer = gem.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(gem.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var aura = go.AddComponent<GemAura>();
        aura.Build((RectTransform)gem.transform);
        return aura;
    }

    public static void Remove(DraggableGem gem)
    {
        Transform existing = gem.transform.Find("Aura");
        if (existing != null) existing.gameObject.SetActive(false);
    }

    private void Build(RectTransform gemRect)
    {
        _selfRect = (RectTransform)transform;
        _gemRect = gemRect;
        _phase = Random.value * 10f;

        Material add = Resources.Load<Material>(Folder + "AuraAdditive");
        Sprite haloSprite = Resources.Load<Sprite>(Folder + "aura_halo");
        Sprite raySprite = Resources.Load<Sprite>(Folder + "aura_rays");
        Sprite sparkSprite = Resources.Load<Sprite>(Folder + "aura_spark");

        _halo = NewImage("Halo", haloSprite, add);
        var halo = _halo.rectTransform;
        halo.anchorMin = Vector2.zero;
        halo.anchorMax = Vector2.one;
        halo.offsetMin = new Vector2(-26f, -26f);
        halo.offsetMax = new Vector2(26f, 26f);

        _rays = NewImage("Rays", raySprite, add);
        _rays.rectTransform.anchorMin = _rays.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        _sparks = new Image[MaxSparks];
        for (int i = 0; i < MaxSparks; i++)
        {
            _sparks[i] = NewImage("Spark" + i, sparkSprite, add);
            var s = _sparks[i].rectTransform;
            s.anchorMin = s.anchorMax = new Vector2(0.5f, 0.5f);
            s.sizeDelta = new Vector2(14f, 14f);
        }
    }

    private Image NewImage(string name, Sprite sprite, Material material)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.material = material;
        img.raycastTarget = false; // an aura must never catch clicks or drops meant for the gem
        return img;
    }

    /// <summary>Sets the look for a tier. Cheap, safe to call whenever the tier changes.</summary>
    public void Apply(int tier, Color tierColor)
    {
        gameObject.SetActive(true);
        _look = LookFor(tier);
        _color = Color.Lerp(tierColor, Color.white, 0.2f);

        for (int i = 0; i < _sparks.Length; i++) _sparks[i].gameObject.SetActive(i < _look.Sparks);
        _rays.gameObject.SetActive(_look.Rays > 0f);
    }

    private void Update()
    {
        if (_halo == null) return;

        float t = Time.unscaledTime + _phase;
        Rect r = _gemRect.rect;
        float w = Mathf.Max(r.width, 1f), h = Mathf.Max(r.height, 1f);

        float pulse = 0.65f + 0.35f * Mathf.Sin(t * _look.Pulse);
        SetColor(_halo, _look.Halo * pulse);

        if (_look.Rays > 0f)
        {
            float size = Mathf.Max(w, h) * 2.3f;
            _rays.rectTransform.sizeDelta = new Vector2(size, size);
            _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * _look.RaySpeed);
            SetColor(_rays, _look.Rays * (0.7f + 0.3f * Mathf.Sin(t * 1.7f)));
        }

        for (int i = 0; i < _look.Sparks; i++)
        {
            float phase = i / (float)_look.Sparks;
            Vector2 pos;
            float alpha;

            if (_look.Rise)
            {
                // embers drifting up the gem, fading in and out
                float progress = (t * 0.45f + phase) % 1f;
                pos = new Vector2((phase * 2f - 1f) * w * 0.6f + Mathf.Sin(t * 2f + i) * 4f, -h * 0.5f + progress * (h + 24f));
                alpha = Mathf.Sin(progress * Mathf.PI);
            }
            else
            {
                float angle = t * 1.1f + phase * Mathf.PI * 2f;
                pos = new Vector2(Mathf.Cos(angle) * (w * 0.5f + 8f), Mathf.Sin(angle) * (h * 0.5f + 8f));
                alpha = 0.6f + 0.4f * Mathf.Sin(t * 5f + i * 1.7f);
            }

            // snap to a 2px grid so the sparkles move in pixel steps like the rest of the art
            pos.x = Mathf.Round(pos.x / 2f) * 2f;
            pos.y = Mathf.Round(pos.y / 2f) * 2f;
            _sparks[i].rectTransform.anchoredPosition = pos;
            SetColor(_sparks[i], alpha);
        }
    }

    private void SetColor(Image img, float alpha)
    {
        img.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(alpha));
    }
}

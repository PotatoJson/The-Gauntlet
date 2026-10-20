using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Little icons floating above an enemy's head for what is affecting it: burning, chilled / frost (with the
/// stack count), shocked, and buffed by a Caster or Elite. Added to every enemy at runtime from a prefab in
/// Resources/EnemyFx, so no enemy prefab needs editing.
/// </summary>
public class EnemyStatusIcons : MonoBehaviour
{
    private const string PrefabPath = "EnemyFx/EnemyStatusIcons";

    [Header("Icons")]
    [SerializeField] private Sprite burnIcon;
    [SerializeField] private Sprite frostIcon;
    [SerializeField] private Sprite joltIcon;
    [SerializeField] private Sprite buffIcon;
    [SerializeField] private TMP_FontAsset stackFont;

    [Header("Layout")]
    [Tooltip("Height above the enemy's feet. The health bar sits at 2.2.")]
    [SerializeField] private float height = 2.75f;
    [SerializeField] private float spacing = 0.6f;

    private enum Kind { Burn, Frost, Jolt, Buff }

    private class Icon
    {
        public Kind kind;
        public Transform root;
        public SpriteRenderer renderer;
        public TextMeshPro stacks;
        public bool shown;
    }

    private BaseEnemy _enemy;
    private StatusManager _status;
    private Transform _camera;
    private readonly List<Icon> _icons = new List<Icon>();
    private readonly List<Icon> _visible = new List<Icon>();

    /// <summary>Gives the enemy its icon display (nothing happens if the prefab isn't there).</summary>
    public static void Attach(BaseEnemy enemy)
    {
        if (enemy == null || enemy.GetComponentInChildren<EnemyStatusIcons>() != null) return;

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, enemy.transform);
        instance.name = "StatusIcons";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
    }

    private void Awake()
    {
        _enemy = GetComponentInParent<BaseEnemy>();
        if (_enemy == null) { enabled = false; return; }

        // Keep the icons the same size whatever the enemy is scaled to.
        float scale = Mathf.Max(0.01f, _enemy.transform.lossyScale.y);
        transform.localScale = Vector3.one / scale;

        Build(Kind.Burn, burnIcon);
        Build(Kind.Frost, frostIcon);
        Build(Kind.Jolt, joltIcon);
        Build(Kind.Buff, buffIcon);
    }

    private void Build(Kind kind, Sprite sprite)
    {
        if (sprite == null) return;

        var root = new GameObject(kind + "Icon").transform;
        root.SetParent(transform, false);
        root.localScale = Vector3.zero;

        var renderer = root.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 200;

        var icon = new Icon { kind = kind, root = root, renderer = renderer };

        if (kind == Kind.Frost)
        {
            var text = new GameObject("Stacks").AddComponent<TextMeshPro>();
            text.transform.SetParent(root, false);
            text.transform.localPosition = new Vector3(0.3f, -0.3f, -0.01f);
            if (stackFont != null) text.font = stackFont;
            text.fontSize = 4.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.35f;
            text.outlineColor = Color.black;
            text.sortingOrder = 201;
            text.text = "";
            icon.stacks = text;
        }

        root.gameObject.SetActive(false);
        _icons.Add(icon);
    }

    private void LateUpdate()
    {
        if (_enemy == null) return;

        if (_enemy.IsDead())
        {
            gameObject.SetActive(false);
            return;
        }

        if (_status == null) _status = _enemy.GetComponent<StatusManager>();

        // Sit above the head (in world space, whatever way the enemy is facing) and face the camera.
        transform.position = _enemy.transform.position + Vector3.up * height;
        Transform cam = GetCamera();
        if (cam != null) transform.rotation = cam.rotation;

        bool burning = false, frozen = false, shocked = false;
        int stacks = 0;
        bool chilled = false;

        if (_status != null)
        {
            foreach (BaseStatusEffect effect in _status.Active)
            {
                switch (effect.StatusID)
                {
                    case "Burn": burning = true; break;
                    case "Slow": chilled = true; break;
                    case "Jolted": shocked = true; break;
                    case "Frost":
                        var frost = effect as FrostStatus;
                        if (frost != null) { stacks = frost.Stacks; frozen = frost.IsFrozen; }
                        break;
                }
            }
        }

        bool frostShown = frozen || stacks > 0 || chilled;

        _visible.Clear();
        foreach (Icon icon in _icons)
        {
            bool want = false;
            switch (icon.kind)
            {
                case Kind.Burn: want = burning; break;
                case Kind.Frost: want = frostShown; break;
                case Kind.Jolt: want = shocked; break;
                case Kind.Buff: want = _enemy.IsBuffed(); break;
            }

            SetShown(icon, want);
            if (want) _visible.Add(icon);

            if (icon.kind == Kind.Frost && icon.stacks != null)
                icon.stacks.text = (!frozen && stacks > 0) ? stacks.ToString() : "";
        }

        // Centre the row.
        float startX = -(_visible.Count - 1) * spacing * 0.5f;
        for (int i = 0; i < _visible.Count; i++)
        {
            Icon icon = _visible[i];
            Vector3 target = new Vector3(startX + i * spacing, 0f, 0f);
            icon.root.localPosition = Vector3.Lerp(icon.root.localPosition, target, 1f - Mathf.Exp(-14f * Time.deltaTime));

            Animate(icon, frozen);
        }
    }

    private void SetShown(Icon icon, bool shown)
    {
        if (icon.shown == shown) return;
        icon.shown = shown;

        icon.root.DOKill();
        if (shown)
        {
            icon.root.gameObject.SetActive(true);
            icon.root.localScale = Vector3.zero;
            icon.root.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack);
        }
        else
        {
            icon.root.DOScale(Vector3.zero, 0.12f).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (icon.root != null && !icon.shown) icon.root.gameObject.SetActive(false);
            });
        }
    }

    // Slightly dimmed so the post-processing bloom doesn't wash the icons out to white blobs.
    private static readonly Color Base = new Color(0.72f, 0.72f, 0.72f, 1f);

    // Each icon idles a little so a row of them reads as "active", not as a sticker.
    private void Animate(Icon icon, bool frozen)
    {
        float t = Time.time;
        Vector3 pos = icon.root.localPosition;

        switch (icon.kind)
        {
            case Kind.Burn:
                icon.renderer.color = Color.Lerp(Base, new Color(0.72f, 0.6f, 0.45f), 0.5f + 0.5f * Mathf.Sin(t * 14f));
                pos.y = Mathf.Sin(t * 9f) * 0.03f;
                break;

            case Kind.Frost:
                icon.renderer.color = frozen
                    ? Color.Lerp(new Color(0.45f, 0.75f, 0.8f), Base, 0.5f + 0.5f * Mathf.Sin(t * 6f))
                    : Base;
                pos.y = Mathf.Sin(t * 3f) * 0.025f;
                break;

            case Kind.Jolt:
                icon.renderer.color = Mathf.Sin(t * 30f) > 0f ? Base : new Color(0.72f, 0.72f, 0.45f);
                pos.x += Random.Range(-0.02f, 0.02f);
                break;

            case Kind.Buff:
                pos.y = Mathf.Sin(t * 4f) * 0.05f;               // bobs up and down like an "up" arrow
                icon.renderer.color = Color.Lerp(Base, new Color(0.72f, 0.55f, 0.5f), 0.5f + 0.5f * Mathf.Sin(t * 5f));
                break;
        }

        icon.root.localPosition = pos;
    }

    private Transform GetCamera()
    {
        if (_camera == null)
        {
            if (PlayerCamera.Instance != null && PlayerCamera.Instance.cameraObject != null)
                _camera = PlayerCamera.Instance.cameraObject.transform;
            else if (Camera.main != null)
                _camera = Camera.main.transform;
        }
        return _camera;
    }
}

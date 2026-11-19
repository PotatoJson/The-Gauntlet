using TMPro;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// A word over an enemy, in the world (SHATTER!, OVERLOAD!): pops in, rises, fades. Faces the camera.
/// </summary>
public class ReactionPopup : MonoBehaviour
{
    private const float Seconds = 0.9f;
    private const float Rise = 1.2f;

    private TextMeshPro _text;
    private Vector3 _from;
    private float _age;
    private Color _colour;
    private float _size = 1f;

    /// <param name="size">1 = normal; the reaction's strength makes it bigger or smaller.</param>
    public static void Show(Vector3 at, string words, Color colour, float size = 1f)
    {
        var popup = new GameObject("ReactionPopup").AddComponent<ReactionPopup>();
        popup._size = size;
        popup._from = at;
        popup._colour = colour;
        popup.transform.position = at;

        TextMeshPro text = popup.gameObject.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = words.ToUpperInvariant();
        text.fontSize = 5f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = colour;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color(0.08f, 0.04f, 0.04f, 1f);
        // Drawn over everything: a wall or the knight's own head shouldn't hide what just happened.
        Shader overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlay != null) text.fontMaterial.shader = overlay;
        // Chinese and Japanese need the CJK-capable font, as everywhere else.
        var font = popup.gameObject.AddComponent<LocalizedFontAsset>();
        font.AssetReference = new LocalizedTmpFont { TableReference = "UIFonts", TableEntryReference = "MainFont" };
        popup._text = text;
    }

    private void LateUpdate()
    {
        _age += Time.unscaledDeltaTime;
        float u = _age / Seconds;
        if (u >= 1f) { Destroy(gameObject); return; }

        transform.position = _from + Vector3.up * (Rise * (1f - (1f - u) * (1f - u)));
        // Pops in big, settles to normal size.
        float pop = u < 0.15f ? Mathf.Lerp(0.4f, 1.25f, u / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((u - 0.15f) / 0.2f));
        transform.localScale = Vector3.one * pop * _size;

        Camera view = Camera.main;
        if (view != null) transform.rotation = Quaternion.LookRotation(transform.position - view.transform.position);

        Color colour = _colour;
        colour.a = u < 0.6f ? 1f : 1f - (u - 0.6f) / 0.4f;
        _text.color = colour;
    }
}

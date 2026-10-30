using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A flat-coloured panel whose left edge leans: the top-left corner sits at the rectangle's edge and the bottom-left
/// corner is pushed in by <see cref="slant"/>, so the shape reads as a slanted tab. The right edge stays square, so a
/// stack of these lines up flush on one side.
///
/// With <see cref="stripWidth"/> above zero only a strip along the slanted edge is drawn (the coloured spine of a
/// card). Works with UI Outline, since it is an ordinary Graphic.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SlantedPanel : MaskableGraphic
{
    [SerializeField] private float _slant = 36f;
    [SerializeField] private float _stripWidth;

    /// <summary>How far the bottom-left corner is pushed in, in canvas units.</summary>
    public float slant
    {
        get => _slant;
        set { if (!Mathf.Approximately(_slant, value)) { _slant = value; SetVerticesDirty(); } }
    }

    /// <summary>0 draws the whole panel; above 0 draws only a strip this wide along the slanted edge.</summary>
    public float stripWidth
    {
        get => _stripWidth;
        set { if (!Mathf.Approximately(_stripWidth, value)) { _stripWidth = value; SetVerticesDirty(); } }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        float lean = Mathf.Min(_slant, r.width * 0.5f);
        Color32 c = color;

        Vector2 topLeft = new Vector2(r.xMin, r.yMax);
        Vector2 bottomLeft = new Vector2(r.xMin + lean, r.yMin);
        Vector2 topRight, bottomRight;

        if (_stripWidth > 0f)
        {
            float w = Mathf.Min(_stripWidth, r.width);
            topRight = new Vector2(r.xMin + w, r.yMax);
            bottomRight = new Vector2(r.xMin + lean + w, r.yMin);
        }
        else
        {
            topRight = new Vector2(r.xMax, r.yMax);
            bottomRight = new Vector2(r.xMax, r.yMin);
        }

        vh.AddVert(topLeft, c, Vector2.zero);
        vh.AddVert(topRight, c, Vector2.zero);
        vh.AddVert(bottomRight, c, Vector2.zero);
        vh.AddVert(bottomLeft, c, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A solid colour UI panel whose left and right edges are sheared, so it reads as the
/// slanted slab used across the Persona menus.
///
/// It draws its own quad, which means the title screen needs no imported sprite or
/// sliced image: colour it, size it and set <see cref="Slant"/>.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[DisallowMultipleComponent]
public class SkewedPanel : MaskableGraphic
{
    [Tooltip("How far the top edge sits to the right of the bottom edge, in UI pixels.")]
    [SerializeField, Range(0f, 200f)] private float slant = 26f;

    public float Slant
    {
        get { return slant; }
        set
        {
            slant = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();

        // Counter-clockwise: bottom-left, top-left, top-right, bottom-right.
        vh.AddVert(new Vector2(rect.xMin, rect.yMin), color, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMin + slant, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMax + slant, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMax, rect.yMin), color, Vector2.zero);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}

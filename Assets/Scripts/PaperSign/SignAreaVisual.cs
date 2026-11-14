using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the sign area as an empty field ruled onto the paper: a slightly wobbly ink outline, a colder
/// guide line just inside it and the faintest darkening of the slot, so it reads as a place waiting to
/// be signed rather than a UI box laid over a document.
///
/// Everything is baked into the RawImage the button already uses, so PaperSign's alpha pulse fades the
/// whole mark as one piece and the Button's tint and hover behaviour keep working untouched.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public class SignAreaVisual : MonoBehaviour
{
    [Header("Mark")]
    [Tooltip("Thickness of the outer ink line, in canvas units.")]
    [SerializeField, Range(0.5f, 4f)] private float inkThickness = 1.6f;

    [Tooltip("How far inside the ink line the cold guide line sits. Zero hides it.")]
    [SerializeField, Range(0f, 14f)] private float guideInset = 4.5f;

    [Tooltip("Thickness of the cold guide line, in canvas units.")]
    [SerializeField, Range(0.3f, 3f)] private float guideThickness = 0.8f;

    [Tooltip("Corner radius of the field, in canvas units. Small values read as hand-ruled.")]
    [SerializeField, Range(0f, 24f)] private float cornerRadius = 4f;

    [Header("Colours")]
    [Tooltip("The ink the outline is drawn with.")]
    [SerializeField] private Color inkColor = new Color(0.07f, 0.07f, 0.08f, 0.9f);

    [Tooltip("The cold line inside the outline, the only colour on the sheet.")]
    [SerializeField] private Color guideColor = new Color(0.33f, 0.45f, 0.56f, 0.55f);

    [Tooltip("A whisper of shade inside the field, so the empty slot reads at a glance.")]
    [SerializeField] private Color fillColor = new Color(0.02f, 0.02f, 0.03f, 0.045f);

    [Header("Hand")]
    [Tooltip("Wobble of the ink line, in canvas units. Larger values look more sketched.")]
    [SerializeField, Range(0f, 3f)] private float wobble = 1.1f;

    [Tooltip("How much the ink thins and thickens along its length, 0-1.")]
    [SerializeField, Range(0f, 1f)] private float inkVariation = 0.35f;

    /// <summary>The field is baked at twice its canvas size, so the strokes stay crisp once scaled.</summary>
    private const float Supersample = 2f;

    private RawImage target;
    private Texture2D texture;
    private Vector2 builtSize;

    private void Awake()
    {
        target = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDestroy()
    {
        Release();
    }

    private void Update()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        // Layout normally settles in one frame; a resolution switch rebuilds once.
        Vector2 size = rect.rect.size;
        if (Mathf.Abs(size.x - builtSize.x) > 1f || Mathf.Abs(size.y - builtSize.y) > 1f)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        RectTransform rect = transform as RectTransform;
        if (target == null || rect == null)
        {
            return;
        }

        Vector2 size = rect.rect.size;
        if (size.x < 4f || size.y < 4f)
        {
            return;
        }

        builtSize = size;
        Release();
        texture = Bake(size);
        target.texture = texture;
    }

    private Texture2D Bake(Vector2 size)
    {
        int width = Mathf.Max(8, Mathf.CeilToInt(size.x * Supersample));
        int height = Mathf.Max(8, Mathf.CeilToInt(size.y * Supersample));

        float scale = width / Mathf.Max(1f, size.x);
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        // The outline is inset by its own thickness so the stroke is never clipped by the edge.
        float margin = Mathf.Max(1f, inkThickness * scale);
        float inkPixels = Mathf.Max(1.2f, inkThickness * scale);
        float guidePixels = Mathf.Max(1f, guideThickness * scale);
        float radius = Mathf.Clamp(cornerRadius * scale, 0f, Mathf.Min(halfWidth, halfHeight));

        float innerHalfWidth = Mathf.Max(1f, halfWidth - margin);
        float innerHalfHeight = Mathf.Max(1f, halfHeight - margin);
        float innerRadius = Mathf.Clamp(radius, 0f, Mathf.Min(innerHalfWidth, innerHalfHeight));

        float insetPixels = guideInset * scale;
        float guideHalfWidth = Mathf.Max(1f, innerHalfWidth - insetPixels);
        float guideHalfHeight = Mathf.Max(1f, innerHalfHeight - insetPixels);
        float guideRadius = Mathf.Clamp(radius - insetPixels, 0f, Mathf.Min(guideHalfWidth, guideHalfHeight));

        float wobblePixels = wobble * scale;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float offsetY = y + 0.5f - halfHeight;
            float noiseX = y * 0.085f;

            for (int x = 0; x < width; x++)
            {
                float offsetX = x + 0.5f - halfWidth;
                float noiseY = x * 0.085f;

                // Two noise fields: one bends the line, the other lets the ink thicken and thin.
                float bend = (Mathf.PerlinNoise(noiseY + 37f, noiseX + 11f) - 0.5f) * 2f * wobblePixels;
                float density = Mathf.PerlinNoise(noiseY * 0.55f + 91f, noiseX * 0.55f + 13f);

                float outline = RoundedBoxDistance(offsetX, offsetY, innerHalfWidth, innerHalfHeight, innerRadius) + bend;

                Color pixel = Color.clear;

                // The empty slot: just enough shade to read as unfilled paper.
                Over(ref pixel, fillColor, Mathf.Clamp01(0.5f - outline));

                if (guideInset > 0f)
                {
                    float guide = RoundedBoxDistance(offsetX, offsetY, guideHalfWidth, guideHalfHeight, guideRadius);
                    Over(ref pixel, guideColor, StrokeCoverage(guide, guidePixels));
                }

                float inkDensity = 1f - inkVariation * Mathf.Abs(density * 2f - 1f);
                Over(ref pixel, inkColor, StrokeCoverage(outline, inkPixels * inkDensity));

                pixels[y * width + x] = pixel;
            }
        }

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "SignAreaField",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    /// <summary>Signed distance to a rounded rectangle; negative inside.</summary>
    private static float RoundedBoxDistance(float x, float y, float halfWidth, float halfHeight, float radius)
    {
        float cornerX = Mathf.Abs(x) - halfWidth + radius;
        float cornerY = Mathf.Abs(y) - halfHeight + radius;
        float outsideX = Mathf.Max(cornerX, 0f);
        float outsideY = Mathf.Max(cornerY, 0f);

        return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
            + Mathf.Min(Mathf.Max(cornerX, cornerY), 0f)
            - radius;
    }

    /// <summary>Coverage of a stroke of the given thickness, antialiased across roughly one texel.</summary>
    private static float StrokeCoverage(float distance, float thickness)
    {
        return Mathf.Clamp01(thickness * 0.5f + 0.5f - Mathf.Abs(distance));
    }

    /// <summary>Straight-alpha source-over, so the layers keep their own colours as they stack.</summary>
    private static void Over(ref Color destination, Color source, float coverage)
    {
        float alpha = source.a * coverage;
        if (alpha <= 0f)
        {
            return;
        }

        float resultAlpha = alpha + destination.a * (1f - alpha);
        if (resultAlpha <= 0f)
        {
            destination = Color.clear;
            return;
        }

        float destinationWeight = destination.a * (1f - alpha);

        destination.r = (source.r * alpha + destination.r * destinationWeight) / resultAlpha;
        destination.g = (source.g * alpha + destination.g * destinationWeight) / resultAlpha;
        destination.b = (source.b * alpha + destination.b * destinationWeight) / resultAlpha;
        destination.a = resultAlpha;
    }

    private void Release()
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }

        texture = null;
    }
}

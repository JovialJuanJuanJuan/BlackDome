using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shrinks the real cursor under a click and springs it back on release.
///
/// The cursor stays the hardware cursor set in Player Settings: this swaps it for pre-scaled
/// copies of the same texture while a button is held, then restores the full-size one. Because
/// every copy keeps the original texture size and a (0, 0) hotspot, the pointer tip never moves,
/// the cursor keeps the platform's own size and smoothing, and there is no second pointer drawn
/// on top of it.
/// </summary>
[DisallowMultipleComponent]
public sealed class CursorClickFx : MonoBehaviour
{
    /// <summary>The cursor texture lives in a Resources folder so it can be loaded anywhere.</summary>
    private const string CursorTextureResource = "game_cursor";

    /// <summary>How small the cursor gets while a button is held.</summary>
    private const float PressedScale = 0.8f;

    /// <summary>Number of pre-scaled cursors between the pressed size and the full size.</summary>
    private const int ScaleSteps = 10;

    // Spring used for the dip and the bounce back. Stiffness is high enough to feel instant,
    // damping is just under critical so the return has a small, lively overshoot.
    private const float SpringStiffness = 1400f;
    private const float SpringDamping = 34f;

    private static CursorClickFx instance;

    private Texture2D[] cursorSteps;
    private int appliedStep = -1;

    private float scale = 1f;
    private float scaleVelocity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("CursorClickFx");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<CursorClickFx>();
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        BuildCursorSteps();
    }

    private void BuildCursorSteps()
    {
        Texture2D source = Resources.Load<Texture2D>(CursorTextureResource);

        if (source == null)
        {
            Debug.LogWarning($"{nameof(CursorClickFx)}: no '{CursorTextureResource}' texture in Resources; leaving the cursor alone.");
            enabled = false;
            return;
        }

        cursorSteps = new Texture2D[ScaleSteps];

        for (int i = 0; i < ScaleSteps; i++)
        {
            float t = ScaleSteps == 1 ? 1f : i / (float)(ScaleSteps - 1);
            float stepScale = Mathf.Lerp(PressedScale, 1f, t);

            cursorSteps[i] = stepScale >= 0.999f ? source : ShrinkCursor(source, stepScale);
        }

        // Start from the cursor the build profile already asks for
        ApplyStep(ScaleSteps - 1);
    }

    /// <summary>
    /// Copies the cursor into a texture of the same size with its image scaled down about the
    /// top-left corner, which is the hotspot, so the tip stays under the pointer.
    /// </summary>
    private static Texture2D ShrinkCursor(Texture2D source, float stepScale)
    {
        int width = source.width;
        int height = source.height;

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = $"{source.name}_{Mathf.RoundToInt(stepScale * 100f)}"
        };

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            // Texture rows go bottom-up, but the scale is anchored to the top-left corner
            float vFromTop = height - y - 0.5f;
            float sourceV = vFromTop / stepScale;

            for (int x = 0; x < width; x++)
            {
                float sourceU = (x + 0.5f) / stepScale;

                if (sourceU >= width || sourceV >= height)
                {
                    pixels[y * width + x] = Color.clear;
                    continue;
                }

                // GetPixelBilinear samples in 0..1 space with v measured from the bottom
                pixels[y * width + x] = source.GetPixelBilinear(
                    sourceU / width,
                    1f - sourceV / height);
            }
        }

        result.SetPixels(pixels);
        result.Apply(false, false);
        return result;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null || cursorSteps == null)
        {
            return;
        }

        bool down = mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed;
        float target = down ? PressedScale : 1f;

        // Semi-implicit spring, unscaled so the effect also reads while the game is paused.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        scaleVelocity += (target - scale) * SpringStiffness * dt;
        scaleVelocity -= scaleVelocity * SpringDamping * dt;
        scale += scaleVelocity * dt;

        float scaled = Mathf.Clamp(scale, PressedScale, 1f);
        float t01 = Mathf.InverseLerp(PressedScale, 1f, scaled);
        ApplyStep(Mathf.RoundToInt(t01 * (ScaleSteps - 1)));
    }

    private void ApplyStep(int step)
    {
        if (step == appliedStep || cursorSteps == null)
        {
            return;
        }

        step = Mathf.Clamp(step, 0, cursorSteps.Length - 1);

        Texture2D cursor = cursorSteps[step];

        if (cursor == null)
        {
            return;
        }

        // Same hotspot the build profile uses, so the hotspot never jumps between steps
        Cursor.SetCursor(cursor, Vector2.zero, CursorMode.Auto);
        appliedStep = step;
    }

    private void OnDisable()
    {
        // Hand the cursor back to whatever the build profile set
        if (cursorSteps != null && cursorSteps.Length > 0)
        {
            Cursor.SetCursor(cursorSteps[cursorSteps.Length - 1], Vector2.zero, CursorMode.Auto);
        }

        if (instance == this)
        {
            instance = null;
        }
    }
}

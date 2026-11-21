using System;
using TMPro;
using UnityEngine;

/// <summary>
/// The look of the typewriter: characters arrive instead of snapping on.
///
/// A newly typed glyph fades in while rising and scaling into place, and takes on a brief tint that
/// settles back to the text's own colour. Punctuation gets an extra beat so a line reads with the
/// rhythm of speech rather than a constant mechanical tick.
///
/// The label always holds the whole line, laid out once. Unrevealed characters are drawn fully
/// transparent instead of being appended to the string, so the wrapping never changes mid-line and
/// the effect is pure per-glyph vertex work on top of a stable layout.
/// </summary>
public sealed class TypewriterReveal
{
    /// <summary>How long a character takes to fade and rise into place, in seconds.</summary>
    public float Duration = 0.16f;

    /// <summary>How far a character rises into place, as a fraction of its own height.</summary>
    public float RiseFraction = 0.16f;

    /// <summary>The size a character starts at before settling to full size.</summary>
    public float StartScale = 0.8f;

    /// <summary>The colour a freshly typed character flashes towards.</summary>
    public Color Tint = new Color(1f, 0.96f, 0.86f);

    /// <summary>How strongly a freshly typed character takes on the tint, 0 to 1.</summary>
    public float TintStrength = 0.5f;

    /// <summary>Extra pause after a full stop, question mark or exclamation mark, in seconds.</summary>
    public float SentencePause = 0.18f;

    /// <summary>Extra pause after a comma, semicolon, colon or dash, in seconds.</summary>
    public float ClausePause = 0.08f;

    TMP_Text label;
    TMP_TextInfo info;
    Vector3[][] baseVertices;
    Color32[][] baseColors;
    float[] revealTimes;
    int shown;

    /// <summary>True while there are still characters fading into place.</summary>
    public bool IsAnimating { get; private set; }

    /// <summary>
    /// Starts a line. The label's text must already be assigned; the line is laid out and hidden
    /// here, before it is ever drawn, so the player never sees the finished sentence first.
    /// </summary>
    public void Begin(TMP_Text text)
    {
        label = text;
        IsAnimating = false;

        if (label == null)
            return;

        // Regenerate from the text so the cached geometry is clean even when a previous line was
        // cut short mid-animation and left its offsets in the mesh.
        label.ForceMeshUpdate(true);

        info = label.textInfo;
        if (info == null)
            return;

        int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
        revealTimes = new float[count];
        shown = 0;
        IsAnimating = count > 0;

        CacheBaseMesh();

        // Nothing of the line is drawn until it has been typed.
        Animate(Time.time);
    }

    /// <summary>Records the moment a character becomes visible.</summary>
    public void Reveal(int index, float time)
    {
        if (revealTimes == null || index < 0 || index >= revealTimes.Length)
            return;

        revealTimes[index] = time;

        if (index >= shown)
            shown = index + 1;
    }

    /// <summary>Draws the characters at their current point in the reveal animation.</summary>
    public void Animate(float now)
    {
        if (!IsAnimating || label == null)
            return;

        info = label.textInfo;
        if (info == null || !RestoreBaseGeometry())
            return;

        float duration = Mathf.Max(0.0001f, Duration);
        bool stillAnimating = false;
        int count = Mathf.Min(info.characterCount, revealTimes.Length);

        for (int i = 0; i < count; i++)
        {
            // Characters that have not been typed yet sit at the start of the animation.
            float progress = i < shown ? (now - revealTimes[i]) / duration : 0f;

            if (progress >= 1f)
                continue;

            stillAnimating = true;

            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible)
                continue;

            int material = character.materialReferenceIndex;
            if (material < 0 || material >= baseVertices.Length || baseVertices[material] == null)
                continue;

            int first = character.vertexIndex;
            Vector3[] baseVerts = baseVertices[material];
            Color32[] baseCols = baseColors[material];
            TMP_MeshInfo mesh = info.meshInfo[material];

            if (baseVerts == null || baseCols == null || mesh.vertices == null || mesh.colors32 == null)
                continue;

            if (first < 0 || first + 3 >= baseVerts.Length || first + 3 >= mesh.vertices.Length || first + 3 >= mesh.colors32.Length)
                continue;

            // Ease out cubic: a quick arrival and a soft settle, rather than a linear slide.
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3f);

            Vector3 centre = (character.bottomLeft + character.topRight) * 0.5f;
            float scale = Mathf.Lerp(StartScale, 1f, eased);
            float rise = (character.topRight.y - character.bottomLeft.y) * RiseFraction * (1f - eased);
            float tint = TintStrength * (1f - eased);

            for (int v = 0; v < 4; v++)
            {
                Vector3 position = baseVerts[first + v];
                position = centre + (position - centre) * scale;
                position.y -= rise;
                mesh.vertices[first + v] = position;

                Color32 baseColor = baseCols[first + v];
                Color faded = Color.Lerp(baseColor, Tint, tint);
                faded.a = baseColor.a / 255f * eased;
                mesh.colors32[first + v] = faded;
            }
        }

        label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

        if (!stillAnimating)
            IsAnimating = false;
    }

    /// <summary>Shows the rest of the line immediately, for a player who skips ahead.</summary>
    public void Finish()
    {
        if (!IsAnimating || label == null)
            return;

        info = label.textInfo;
        if (info != null && RestoreBaseGeometry())
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

        IsAnimating = false;
    }

    /// <summary>The extra beat to hold after a character, so the line breathes like speech.</summary>
    public float PauseAfter(char character, char nextCharacter)
    {
        switch (character)
        {
            // An ellipsis is a single beat, not one beat per dot.
            case '.':
                return nextCharacter == '.' ? 0f : SentencePause;
            case '!':
            case '?':
            case '\u2026':
                return SentencePause;
            case ',':
            case ';':
            case ':':
            case '\u2014':
            case '\u2013':
                return ClausePause;
            default:
                return 0f;
        }
    }

    void CacheBaseMesh()
    {
        int materials = info.meshInfo.Length;
        baseVertices = new Vector3[materials][];
        baseColors = new Color32[materials][];

        for (int m = 0; m < materials; m++)
        {
            TMP_MeshInfo mesh = info.meshInfo[m];
            if (mesh.vertices == null || mesh.colors32 == null)
                continue;

            baseVertices[m] = (Vector3[])mesh.vertices.Clone();
            baseColors[m] = (Color32[])mesh.colors32.Clone();
        }
    }

    /// <summary>
    /// Puts every vertex back the way the label laid it out, so each frame animates from the same
    /// clean geometry instead of accumulating offsets frame after frame.
    /// </summary>
    bool RestoreBaseGeometry()
    {
        if (baseVertices == null || baseColors == null || info.meshInfo == null)
            return false;

        bool restored = false;
        int materials = Mathf.Min(baseVertices.Length, info.meshInfo.Length);

        for (int m = 0; m < materials; m++)
        {
            Vector3[] vertices = baseVertices[m];
            Color32[] colors = baseColors[m];
            if (vertices == null || colors == null)
                continue;

            TMP_MeshInfo mesh = info.meshInfo[m];
            if (mesh.vertices == null || mesh.colors32 == null)
                continue;

            Array.Copy(vertices, mesh.vertices, Mathf.Min(vertices.Length, mesh.vertices.Length));
            Array.Copy(colors, mesh.colors32, Mathf.Min(colors.Length, mesh.colors32.Length));
            restored = true;
        }

        return restored;
    }
}

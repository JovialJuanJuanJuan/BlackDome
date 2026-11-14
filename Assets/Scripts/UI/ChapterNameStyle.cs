using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives the chapter card the same cold, quiet treatment the rest of Black Dome's interface uses:
/// the letters cool from white at the top to steel blue at the foot, open up slightly in tracking,
/// and carry a thin cold rim so the title looks lit out of the darkness instead of pasted on it.
///
/// The scene's Animator still owns the presentation: it fades <c>m_fontColor.a</c>, which multiplies
/// the gradient, so the card rises and sinks exactly as it did before.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class ChapterNameStyle : MonoBehaviour
{
    [Header("Gradient")]
    [Tooltip("Colour at the top of the letters, where the light falls.")]
    [SerializeField] private Color topColor = Color.white;

    [Tooltip("Colour at the foot of the letters, where they sink back into the dark.")]
    [SerializeField] private Color bottomColor = new Color(0.44f, 0.55f, 0.66f, 1f);

    [Header("Type")]
    [Tooltip("Extra space between letters, in font units. Small values only: the title wraps.")]
    [SerializeField, Range(-20f, 30f)] private float tracking = 4f;

    [Tooltip("Tightens the two lines of the title.")]
    [SerializeField, Range(-60f, 20f)] private float lineSpacing = -30f;

    [Header("Rim")]
    [Tooltip("The cold edge that traces each letter.")]
    [SerializeField] private Color rimColor = new Color(0.28f, 0.40f, 0.52f, 0.32f);

    [Tooltip("How far the rim spreads, in pixels.")]
    [SerializeField, Range(0f, 4f)] private float rimDistance = 1.6f;

    private void Awake()
    {
        TMP_Text title = GetComponent<TMP_Text>();
        if (title == null)
        {
            return;
        }

        // The scene's colour mode is already four-corners, so white at the top of each letter and
        // steel at the foot reads as one clean vertical falloff; TMP_Text exposes no mode setter.
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(topColor, topColor, bottomColor, bottomColor);

        title.characterSpacing = tracking;
        title.lineSpacing = lineSpacing;
        title.raycastTarget = false;

        Outline rim = GetComponent<Outline>();
        if (rim == null)
        {
            rim = gameObject.AddComponent<Outline>();
        }

        rim.effectColor = rimColor;
        rim.effectDistance = new Vector2(rimDistance, -rimDistance);
        rim.useGraphicAlpha = true;
    }
}

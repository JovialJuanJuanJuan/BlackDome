using UnityEngine;

/// <summary>
/// Remembers the authored size and the canvas group of one character expression sprite, so
/// the switching animation can pop it around and blend it without losing where it started.
/// Added automatically the first time a sprite is used.
/// </summary>
[DisallowMultipleComponent]
public class ExpressionSprite : MonoBehaviour
{
    CanvasGroup group;
    RectTransform rect;
    Vector3 baseScale = Vector3.one;
    Vector2 basePosition;
    bool captured;

    public CanvasGroup Group
    {
        get
        {
            Capture();
            return group;
        }
    }

    /// <summary>Scales the sprite relative to its authored size (1 = untouched).</summary>
    public void SetScale(float multiplier)
    {
        Capture();
        transform.localScale = baseScale * multiplier;
    }

    /// <summary>
    /// True once the sprite has been on screen. Its entrance Animator is only allowed to play on
    /// that first appearance, so the flag keeps the entrance from replaying on later switches.
    /// </summary>
    public bool HasAppeared { get; set; }

    /// <summary>How far the sprite currently sits from its authored spot.</summary>
    public float OffsetX
    {
        get
        {
            Capture();
            return rect != null ? rect.anchoredPosition.x - basePosition.x : 0f;
        }
    }

    /// <summary>
    /// Slides the sprite sideways from where it was authored, which is how the stage moves a
    /// listening character out of the speaker's way.
    /// </summary>
    public void SetOffsetX(float offset)
    {
        Capture();
        if (rect != null)
            rect.anchoredPosition = basePosition + new Vector2(offset, 0f);
    }

    /// <summary>Puts the sprite back where it was authored.</summary>
    public void RestorePosition()
    {
        Capture();
        if (rect != null)
            rect.anchoredPosition = basePosition;
    }

    /// <summary>Puts the sprite back to its authored size.</summary>
    public void RestoreScale()
    {
        Capture();
        transform.localScale = baseScale;
    }

    void Capture()
    {
        if (captured)
            return;

        baseScale = transform.localScale;

        rect = transform as RectTransform;
        if (rect != null)
            basePosition = rect.anchoredPosition;

        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        // The sprites are display-only, so they must not swallow clicks
        group.interactable = false;
        group.blocksRaycasts = false;

        captured = true;
    }

    public static ExpressionSprite For(GameObject target)
    {
        if (target == null)
            return null;

        ExpressionSprite sprite = target.GetComponent<ExpressionSprite>();
        if (sprite == null)
            sprite = target.AddComponent<ExpressionSprite>();

        return sprite;
    }
}

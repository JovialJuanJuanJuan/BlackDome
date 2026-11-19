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
    Vector3 baseScale = Vector3.one;
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

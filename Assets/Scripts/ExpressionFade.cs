using System.Collections;
using UnityEngine;

/// <summary>
/// Visual-novel expression switching. Changing expression is a little beat rather than a
/// plain dissolve: the sprite that leaves shrinks back and fades away, then the new one
/// arrives slightly oversized and snaps down into place as it fades in.
///
/// The first appearance of a character gets the full pop; changing expression while the
/// character is already on screen gets a much smaller version of the same motion, so
/// repeated changes stay calm instead of bouncing on every line.
/// </summary>
public static class ExpressionFade
{
    /// <summary>How long the first, full-strength appearance takes.</summary>
    public const float SwitchDuration = 0.42f;

    /// <summary>Expression changes while the character is already on screen are quicker.</summary>
    public const float RepeatDuration = 0.30f;

    // The switch is a short timeline: the old sprite leaves first, the new one lands on top
    const float OutPhase = 0.33f;      // fraction of the switch spent clearing the old sprite
    const float InPhaseStart = 0.22f;  // fraction at which the new sprite starts arriving

    // First appearance: a proper pop
    const float EntranceShrink = 0.90f;     // the leaving sprite steps back
    const float EntranceOvershoot = 1.10f;  // the arriving sprite lands noticeably too big

    // Later changes: just a hint of the same motion
    const float RepeatShrink = 0.975f;
    const float RepeatOvershoot = 1.035f;

    /// <summary>
    /// True when the sprite is switched on and fully opaque, so re-showing it is a no-op.
    /// </summary>
    public static bool IsVisible(GameObject target)
    {
        if (target == null || !target.activeSelf)
            return false;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group == null || group.alpha >= 0.999f;
    }

    /// <summary>
    /// Some sprites (Agnes's neutral one) carry an Animator that plays the character's entrance,
    /// and expressions are switched by turning the sprite objects on and off. Left alone the
    /// entrance would therefore replay every time the expression came back to that sprite, so it
    /// is allowed to play on the sprite's first appearance in the scene and switched off after.
    /// </summary>
    static void PrepareEntranceAnimation(GameObject target)
    {
        if (target == null)
            return;

        ExpressionSprite sprite = ExpressionSprite.For(target);
        if (sprite == null)
            return;

        if (target.activeSelf)
        {
            // Already on screen without us switching it on, so its entrance is behind it.
            sprite.HasAppeared = true;
            return;
        }

        Animator animator = target.GetComponent<Animator>();
        if (animator != null && sprite.HasAppeared)
            animator.enabled = false;

        sprite.HasAppeared = true;
    }

    /// <summary>
    /// Shows a sprite at full opacity and authored size with no animation (resets, scene starts).
    /// </summary>
    public static void ShowInstantly(GameObject target)
    {
        if (target == null)
            return;

        ExpressionSprite sprite = ExpressionSprite.For(target);
        PrepareEntranceAnimation(target);
        sprite.RestoreScale();
        sprite.Group.alpha = 1f;
        target.SetActive(true);
    }

    /// <summary>
    /// Hides a sprite immediately and puts its size back, ready for a later switch.
    /// </summary>
    public static void HideInstantly(GameObject target)
    {
        if (target == null)
            return;

        ExpressionSprite sprite = ExpressionSprite.For(target);
        sprite.RestoreScale();
        sprite.Group.alpha = 0f;
        target.SetActive(false);
    }

    /// <summary>
    /// Switches to <paramref name="target"/>. When nobody else is on screen this is the
    /// character's entrance and gets the full pop; when another sprite is already showing,
    /// it is a quiet expression change with only a slight shift in size.
    /// </summary>
    public static IEnumerator CrossFade(GameObject[] sprites, GameObject target, float entranceDuration)
    {
        if (target == null)
            yield break;

        if (sprites == null || sprites.Length == 0)
            sprites = new GameObject[] { target };

        if (entranceDuration <= 0f)
        {
            ShowInstantly(target);
            yield break;
        }

        ExpressionSprite incoming = ExpressionSprite.For(target);
        bool wasActive = target.activeSelf;
        PrepareEntranceAnimation(target);
        target.SetActive(true);
        if (!wasActive)
            incoming.Group.alpha = 0f;

        int count = sprites.Length;
        ExpressionSprite[] states = new ExpressionSprite[count];
        float[] startAlphas = new float[count];

        for (int i = 0; i < count; i++)
        {
            if (sprites[i] == null)
                continue;

            states[i] = ExpressionSprite.For(sprites[i]);
            states[i].RestoreScale();  // every switch starts from the authored size
            startAlphas[i] = states[i].Group.alpha;
        }

        // Nobody else on screen means this is the character arriving, not just a new face
        bool entrance = true;
        for (int i = 0; i < count; i++)
        {
            if (states[i] == null || sprites[i] == target)
                continue;

            if (startAlphas[i] > 0.01f)
            {
                entrance = false;
                break;
            }
        }

        float duration = entrance ? entranceDuration : Mathf.Min(entranceDuration, RepeatDuration);
        float overshoot = entrance ? EntranceOvershoot : RepeatOvershoot;
        float shrink = entrance ? EntranceShrink : RepeatShrink;

        float outEnd = Mathf.Max(0.0001f, duration * OutPhase);
        float inStart = duration * InPhaseStart;
        float inLength = Mathf.Max(0.0001f, duration - inStart);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                if (states[i] == null)
                    continue;

                if (sprites[i] == target)
                {
                    // Arrive: fade up quickly, size snaps down with a small overshoot
                    float t = Mathf.Clamp01((elapsed - inStart) / inLength);
                    states[i].Group.alpha = Mathf.Lerp(startAlphas[i], 1f, EaseOutCubic(t));
                    states[i].SetScale(Mathf.LerpUnclamped(overshoot, 1f, EaseOutBack(t)));
                }
                else
                {
                    // Leave: shrink back and fade out early, so the two never fight
                    float t = Mathf.Clamp01(elapsed / outEnd);
                    float eased = t * t;
                    states[i].Group.alpha = Mathf.Lerp(startAlphas[i], 0f, eased);
                    states[i].SetScale(Mathf.Lerp(1f, shrink, eased));
                }
            }

            yield return null;
        }

        Finish(sprites, states, target);
    }

    // Settles every sprite on its final state so nothing is left half sized or half faded.
    static void Finish(GameObject[] sprites, ExpressionSprite[] states, GameObject target)
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            if (states[i] == null)
                continue;

            states[i].RestoreScale();

            if (sprites[i] == target)
            {
                states[i].Group.alpha = 1f;
            }
            else
            {
                states[i].Group.alpha = 0f;
                if (sprites[i] != null)
                    sprites[i].SetActive(false);
            }
        }
    }

    static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    // Overshoots slightly past the target before settling, which gives the "snap" of a pop
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

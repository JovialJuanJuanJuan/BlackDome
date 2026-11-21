using System.Collections;
using UnityEngine;

/// <summary>
/// Owns one character's expression sprites and the switch between them. Dialogue scripts only
/// say which sprite they want; the remembering, the blend, the cancellation of an in-flight
/// switch and the reset all live here instead of being repeated in every script.
/// </summary>
public class ExpressionSet
{
    readonly MonoBehaviour runner;
    readonly GameObject[] sprites;

    GameObject current;
    Coroutine switchRoutine;
    float targetAlpha = 1f;

    public ExpressionSet(MonoBehaviour runner, GameObject[] sprites)
    {
        this.runner = runner;
        this.sprites = sprites;
    }

    /// <summary>The sprite currently on screen, or null when nobody is shown.</summary>
    public GameObject Current
    {
        get { return current; }
    }

    /// <summary>
    /// Opacity this set settles at. The stage lowers it for the character who is listening, so an
    /// expression change while listening arrives dimmed instead of flashing bright.
    /// </summary>
    public float TargetAlpha
    {
        get { return targetAlpha; }
        set { targetAlpha = Mathf.Clamp01(value); }
    }

    /// <summary>True while an expression switch is still animating.</summary>
    public bool IsSwitching
    {
        get { return switchRoutine != null; }
    }

    /// <summary>Switches to <paramref name="target"/>, unless it is already showing.</summary>
    public void Show(GameObject target)
    {
        Show(target, ExpressionFade.SwitchDuration);
    }

    public void Show(GameObject target, float duration)
    {
        if (target == null)
            return;

        if (target == current && ExpressionFade.IsVisible(target, targetAlpha))
            return;

        StopSwitch();
        current = target;
        switchRoutine = runner.StartCoroutine(SwitchTo(target, duration));
    }

    IEnumerator SwitchTo(GameObject target, float duration)
    {
        yield return ExpressionFade.CrossFade(sprites, target, duration, () => targetAlpha);
        switchRoutine = null;
    }

    /// <summary>Takes every sprite off screen at once (scene starts, cut-aways).</summary>
    public void HideAll()
    {
        StopSwitch();
        current = null;

        foreach (GameObject sprite in sprites)
            ExpressionFade.HideInstantly(sprite);
    }

    /// <summary>
    /// Brings the sprite that is on screen to full opacity and size, for when it is about to
    /// fade away and should leave from a settled pose rather than mid-blend.
    /// </summary>
    public void Settle()
    {
        StopSwitch();

        if (current != null && current.activeSelf)
            ExpressionFade.ShowInstantly(current);
    }

    void StopSwitch()
    {
        if (switchRoutine == null)
            return;

        runner.StopCoroutine(switchRoutine);
        switchRoutine = null;
    }
}

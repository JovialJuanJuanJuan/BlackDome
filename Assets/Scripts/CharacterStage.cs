using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps a scene's two speaking characters on stage together, the way a visual novel does it.
/// While a scene has only one character, that character keeps the usual centred mark. Once a
/// second character walks in, the pair takes the left and right marks with a clear gap down the
/// middle so they never overlap. Whoever is talking is fully lit and the other is dimmed; on
/// narration, when nobody is talking, both are dimmed.
/// </summary>
public class CharacterStage
{
    /// <summary>Opacity of a character who is not talking.</summary>
    public const float ListenerAlpha = 0.45f;

    /// <summary>How long the move between the centred and paired marks takes.</summary>
    public const float SettleDuration = 0.28f;

    // The paired marks are measured from the sprite art: Agnes reaches 393 units left and 349
    // right of her pivot, Morgana 284/276 and Ryder 258/258. These positions leave a gap of
    // roughly 130-150 units between the two silhouettes on the 1920-wide canvas.
    public const float LeftHome = -320f;
    public const float RightHome = 440f;

    /// <summary>One character on the stage: their expression sprites and their paired mark.</summary>
    public class Slot
    {
        public readonly string Name;
        public readonly ExpressionSet Set;
        public readonly float Home;   // horizontal offset used once a second character is present

        public Slot(string name, ExpressionSet set, float home)
        {
            Name = name;
            Set = set;
            Home = home;
        }
    }

    readonly MonoBehaviour runner;
    readonly Slot[] slots;
    Slot speaker;
    Coroutine settleRoutine;

    public CharacterStage(MonoBehaviour runner, params Slot[] slots)
    {
        this.runner = runner;
        this.slots = slots;
    }

    /// <summary>True while a character's sprite is actually switched on.</summary>
    public static bool IsOnStage(Slot slot)
    {
        return slot != null && slot.Set.Current != null && slot.Set.Current.activeSelf;
    }

    /// <summary>
    /// Puts a character's new expression on stage. A lone character stays centred; with a second
    /// character present, the new sprite appears straight on its own side.
    /// </summary>
    public void Show(string character, GameObject sprite, float duration)
    {
        Slot slot = Find(character);
        if (slot == null)
            return;

        slot.Set.Show(sprite, duration);
        SetOffset(slot, MarkFor(slot));
        slot.Set.TargetAlpha = AlphaFor(slot);
    }

    /// <summary>
    /// Lights the character who is talking. Pass an empty name on narration lines: with nobody
    /// speaking every character on stage is dimmed.
    /// </summary>
    public void Focus(string character)
    {
        Slot next = Find(character);
        speaker = IsOnStage(next) ? next : null;

        if (settleRoutine != null)
            runner.StopCoroutine(settleRoutine);

        settleRoutine = runner.StartCoroutine(SettleLayout());
    }

    // One character keeps the centre; two share the frame from their own marks.
    float MarkFor(Slot slot)
    {
        return CharactersOnStage() >= 2 ? slot.Home : 0f;
    }

    Slot Find(string character)
    {
        if (string.IsNullOrEmpty(character))
            return null;

        foreach (Slot slot in slots)
        {
            if (slot != null && slot.Name == character)
                return slot;
        }

        return null;
    }

    int CharactersOnStage()
    {
        int count = 0;
        foreach (Slot slot in slots)
        {
            if (IsOnStage(slot))
                count++;
        }

        return count;
    }

    float AlphaFor(Slot slot)
    {
        if (speaker == slot)
            return 1f;

        // Alone on stage, a character stays readable even while the narrator is talking.
        return CharactersOnStage() >= 2 ? ListenerAlpha : 1f;
    }

    IEnumerator SettleLayout()
    {
        int count = slots.Length;
        float[] startAlphas = new float[count];
        float[] startOffsets = new float[count];

        for (int i = 0; i < count; i++)
        {
            startAlphas[i] = AlphaOf(slots[i]);
            startOffsets[i] = OffsetOf(slots[i]);
        }

        float elapsed = 0f;
        while (elapsed < SettleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SettleDuration));
            Step(startAlphas, startOffsets, t);
            yield return null;
        }

        Step(startAlphas, startOffsets, 1f);
        settleRoutine = null;
    }

    void Step(float[] startAlphas, float[] startOffsets, float t)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (!IsOnStage(slot))
                continue;

            slot.Set.TargetAlpha = AlphaFor(slot);

            SetOffset(slot, Mathf.Lerp(startOffsets[i], MarkFor(slot), t));

            // A sprite that is currently switching fades itself in at the right opacity.
            if (slot.Set.IsSwitching)
                continue;

            SetAlpha(slot, Mathf.Lerp(startAlphas[i], slot.Set.TargetAlpha, t));
        }
    }

    static float OffsetOf(Slot slot)
    {
        if (!IsOnStage(slot))
            return 0f;

        return ExpressionSprite.For(slot.Set.Current).OffsetX;
    }

    static float AlphaOf(Slot slot)
    {
        if (!IsOnStage(slot))
            return 1f;

        return ExpressionSprite.For(slot.Set.Current).Group.alpha;
    }

    static void SetOffset(Slot slot, float offset)
    {
        if (!IsOnStage(slot))
            return;

        ExpressionSprite.For(slot.Set.Current).SetOffsetX(offset);
    }

    static void SetAlpha(Slot slot, float alpha)
    {
        if (!IsOnStage(slot))
            return;

        ExpressionSprite.For(slot.Set.Current).Group.alpha = alpha;
    }
}

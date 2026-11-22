using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Types a dialogue line into the text box.
///
/// The whole line is handed to the label up front and laid out once, so the wrapping never changes
/// while it types. Characters are uncovered one at a time on a timer, so the beat is even at any
/// frame rate, and each new one fades and rises into place through <see cref="TypewriterReveal"/>.
///
/// A line can carry its own pacing: a mood marker such as [slow] or [urgent] changes the typing
/// speed and the holds after punctuation, and [beat] / [pause=0.8] drop a silence anywhere in the
/// line. See <see cref="TypewriterPacing"/>; the markers never reach the player.
/// </summary>
public class TextCreator : MonoBehaviour
{
    public static TMP_Text viewText;
    public static bool runTextPrint;
    public static int charCount;

    /// <summary>
    /// How many dialogue lines this scene has started. The save system records it so a loaded
    /// scene can be replayed up to the line the player left.
    /// </summary>
    public static int SceneLineCount { get; private set; }

    /// <summary>
    /// Set while a loaded scene is being fast-forwarded: every line is shown whole at once so the
    /// replay does not spend real time typing out dialogue the player has already read.
    /// </summary>
    public static bool InstantMode { get; set; }

    /// <summary>The last raw line handed to the typewriter, pacing markers included.</summary>
    public static string LatestLine { get; private set; }

    /// <summary>Called by the save system when a scene loads, so line counting starts from zero.</summary>
    public static void ResetSceneLineCount()
    {
        SceneLineCount = 0;
        LatestLine = string.Empty;
    }

    static string pendingLine;

    [SerializeField] string transferText;
    [SerializeField] int internalCount;

    [Header("Typing")]
    [Tooltip("Seconds between characters at normal speed. A mood marker scales this.")]
    [SerializeField] float charDelay = 0.03f;

    [Tooltip("Extra beat after a full stop, question mark or exclamation mark, on lines with no mood.")]
    [SerializeField] float sentencePause = 0.18f;

    [Tooltip("Extra beat after a comma, semicolon, colon or dash, on lines with no mood.")]
    [SerializeField] float clausePause = 0.08f;

    [Header("Reveal Animation")]
    [Tooltip("How long each character takes to fade and rise into place, at normal speed.")]
    [SerializeField] float revealDuration = 0.16f;

    [Tooltip("How far a character rises into place, as a fraction of its own height.")]
    [SerializeField] float revealRise = 0.16f;

    [Tooltip("The size a character starts at before settling to full size.")]
    [SerializeField] float revealStartScale = 0.8f;

    [Tooltip("Colour a freshly typed character flashes towards.")]
    [SerializeField] Color revealTint = new Color(1f, 0.96f, 0.86f);

    [Range(0f, 1f)]
    [Tooltip("How strongly a freshly typed character takes on the tint.")]
    [SerializeField] float revealTintStrength = 0.5f;

    readonly TypewriterReveal reveal = new TypewriterReveal();

    TMP_Text label;
    Coroutine rollRoutine;

    /// <summary>
    /// Starts typing a line. Pass the raw dialogue string, pacing markers included, so the
    /// typewriter can read the mood and the holds out of it.
    /// </summary>
    public static void Run(string line)
    {
        NoteLineStarted(line);
        pendingLine = line;
        runTextPrint = true;
    }

    /// <summary>
    /// Records that a line has begun. The typewriter calls this itself; a scene that types its own
    /// dialogue (the intro sequence) calls it so saves made there still point at a line.
    /// </summary>
    public static void NoteLineStarted(string line)
    {
        SceneLineCount++;
        LatestLine = line;
    }

    void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    // LateUpdate, not Update: the dialogue scripts hand over the line from a coroutine, which runs
    // after Update but before LateUpdate. Picking it up here means the line is laid out and hidden
    // in the same frame it was assigned, so the finished sentence never flashes on screen first.
    void LateUpdate()
    {
        internalCount = charCount;

        if (label == null)
            return;

        if (runTextPrint)
        {
            runTextPrint = false;
            viewText = label;

            // A new line can be requested before the previous one has finished typing.
            if (rollRoutine != null)
                StopCoroutine(rollRoutine);

            string rawLine = pendingLine;
            pendingLine = null;

            // Anything that still starts the typewriter the old way just types what the label holds.
            if (rawLine == null)
                rawLine = viewText.text;

            rollRoutine = StartCoroutine(RollText(rawLine));
        }

        if (reveal.IsAnimating)
            reveal.Animate(Time.time);
    }

    IEnumerator RollText(string rawLine)
    {
        TypewriterLine line = TypewriterPacing.Parse(rawLine);
        string body = line.Body;
        transferText = body;

        // A mood speeds up or slows down both the letters and their fade.
        float speed = Mathf.Max(0.35f, line.Speed);
        float gap = charDelay / speed;

        viewText.text = body;

        if (InstantMode)
        {
            // Fast-forwarding a loaded save: show the line whole and let the caller move on.
            int instantCount = viewText.textInfo != null ? viewText.textInfo.characterCount : 0;
            charCount = Mathf.Max(body.Length, instantCount);
            internalCount = charCount;
            rollRoutine = null;
            yield break;
        }

        reveal.Duration = Mathf.Clamp(revealDuration / speed, 0.05f, 0.3f);
        reveal.RiseFraction = revealRise;
        reveal.StartScale = revealStartScale;
        reveal.Tint = revealTint;
        reveal.TintStrength = revealTintStrength;
        reveal.SentencePause = line.HasMood ? line.SentencePause : sentencePause;
        reveal.ClausePause = line.HasMood ? line.ClausePause : clausePause;

        // Lays the line out once and keeps it hidden until it has been typed.
        reveal.Begin(viewText);

        charCount = 0;
        internalCount = 0;

        int total = viewText.textInfo.characterCount;
        if (total == 0)
        {
            // Nothing to type (or the box is not laid out yet): don't leave the caller waiting.
            charCount = body.Length;
            internalCount = charCount;
            rollRoutine = null;
            yield break;
        }

        float tailHold = line.HoldAt(total);
        int index = 0;
        float wait = 0f;

        // A hold written before the first word is a silence that opens the line.
        float leadHold = line.HoldAt(0);
        if (leadHold > 0f)
            yield return new WaitForSeconds(leadHold);

        while (index < total)
        {
            // Count down the gap to the next character, and type it in the frame that gap ends.
            if (wait > 0f)
            {
                wait -= Time.deltaTime;
                if (wait > 0f)
                {
                    yield return null;
                    continue;
                }
            }

            reveal.Reveal(index, Time.time);
            index++;

            // A hold written at the very end of the line is still part of the line: keep the
            // scripts waiting until the silence has played out.
            bool endingOnHold = index >= total && tailHold > 0f;
            charCount = endingOnHold ? index - 1 : index;
            internalCount = charCount;

            TMP_TextInfo info = viewText.textInfo;
            char character = info.characterInfo[index - 1].character;
            char nextCharacter = index < total ? info.characterInfo[index].character : '\0';
            wait = gap + reveal.PauseAfter(character, nextCharacter) + line.HoldAt(index);

            yield return null;
        }

        if (tailHold > 0f)
            yield return new WaitForSeconds(tailHold);

        // Compare against the string length as well, so callers waiting on charCount always finish
        // even if the visible character count differs from what they handed over.
        charCount = Mathf.Max(total, body.Length);
        internalCount = charCount;
        rollRoutine = null;
    }
}

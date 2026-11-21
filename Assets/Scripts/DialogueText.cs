using TMPro;

/// <summary>
/// How narration is told apart from speech. Lines that describe the world or Bay's own thoughts
/// are written with a leading '~' in the dialogue scripts; the marker is never displayed, and the
/// line is shown in italics with no speaker name, so the player still reads it as the narrator's
/// voice rather than a character talking.
/// </summary>
public static class DialogueText
{
    /// <summary>Marks a line as narration in the scripts. It is stripped before the line is shown.</summary>
    public const char NarrationMarker = '~';

    /// <summary>True when the line is the narrator's, not a character speaking.</summary>
    public static bool IsNarration(string line)
    {
        return !string.IsNullOrEmpty(line) && line[0] == NarrationMarker;
    }

    /// <summary>The line as the player sees it: the narration marker removed.</summary>
    public static string Body(string line)
    {
        return IsNarration(line) ? line.Substring(1) : line;
    }

    /// <summary>
    /// True when the whole line is a quotation: somebody is talking, but there is no nameplate to
    /// say who. Those lines read as speech even though they have no speaker name.
    /// </summary>
    public static bool IsQuotedSpeech(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        string trimmed = line.Trim();
        if (trimmed.Length < 2)
            return false;

        char first = trimmed[0];
        char last = trimmed[trimmed.Length - 1];

        return (first == '"' && last == '"')
            || (first == '\u201C' && last == '\u201D');
    }

    /// <summary>
    /// Italics mark the narrator. A line is narration when it carries the '~' marker, or when it is
    /// shown with no speaker name at all, unless it is a quotation of somebody talking.
    /// </summary>
    public static bool ShouldUseItalics(string line, string speakerName)
    {
        if (IsNarration(line))
            return true;

        return string.IsNullOrEmpty(speakerName) && !IsQuotedSpeech(line);
    }

    /// <summary>
    /// Narration is set in italics and speech is upright, so a line with no speaker name still
    /// reads as the narrator rather than as somebody talking off-screen.
    /// </summary>
    public static void ApplyStyle(TMP_Text label, bool italics)
    {
        if (label == null)
            return;

        label.fontStyle = italics ? FontStyles.Italic : FontStyles.Normal;
    }
}

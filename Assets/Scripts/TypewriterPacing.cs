using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Per-line pacing for the typewriter, written into the dialogue strings themselves.
///
/// A line may open with a mood and may hold its breath anywhere inside it:
///
///   "~[slow]He turned the handle."
///   "[tense]Nothing... [pause=0.9]not a sound."
///   "[urgent]Run![beat]Now!"
///
/// The markers are removed before the line is shown, so the player never sees them; they only
/// decide how fast the line is typed and where it pauses. Recognised moods are slow, soft, calm,
/// tense and urgent. [beat] adds a short hold, [pause] a longer one, and [pause=SECONDS] an exact
/// one, at the point in the line where the marker sits.
/// </summary>
public static class TypewriterPacing
{
    public const float DefaultSentencePause = 0.18f;
    public const float DefaultClausePause = 0.08f;

    /// <summary>A short dramatic hold, for [beat].</summary>
    public const float BeatPause = 0.35f;

    /// <summary>A longer held silence, for [pause].</summary>
    public const float ShortPause = 0.6f;

    /// <summary>Reads a raw dialogue line into its visible text and its pacing.</summary>
    public static TypewriterLine Parse(string raw)
    {
        TypewriterLine line = new TypewriterLine();
        if (string.IsNullOrEmpty(raw))
            return line;

        StringBuilder body = new StringBuilder(raw.Length);

        // The narration marker is only a marker while nothing has been written yet.
        bool leading = true;
        int i = 0;

        while (i < raw.Length)
        {
            char current = raw[i];

            if (current == '[')
            {
                int close = raw.IndexOf(']', i + 1);
                if (close > i + 1)
                {
                    string token = raw.Substring(i + 1, close - i - 1);
                    if (ApplyToken(line, token, body.Length))
                    {
                        i = close + 1;
                        continue;
                    }
                }
            }

            if (leading && current == DialogueText.NarrationMarker)
            {
                line.Narration = true;
                i++;
                continue;
            }

            body.Append(current);
            leading = false;
            i++;
        }

        line.Body = body.ToString();
        return line;
    }

    /// <summary>The line as the player sees it, with every marker removed.</summary>
    public static string Display(string raw)
    {
        return Parse(raw).Body;
    }

    static bool ApplyToken(TypewriterLine line, string token, int position)
    {
        string key = token.Trim().ToLowerInvariant();
        if (key.Length == 0)
            return false;

        switch (key)
        {
            // Moods set the whole line's pace: the gap between characters, and how long a full
            // stop and a comma are held. Quick words with long sentence holds read as tension.
            case "slow":
                line.SetMood(0.6f, 0.5f, 0.2f);
                return true;
            case "soft":
                line.SetMood(0.72f, 0.4f, 0.16f);
                return true;
            case "calm":
                line.SetMood(0.85f, 0.28f, 0.1f);
                return true;
            case "tense":
                line.SetMood(1.2f, 0.55f, 0.16f);
                return true;
            case "urgent":
                line.SetMood(1.7f, 0.25f, 0.07f);
                return true;

            case "beat":
                line.AddHold(position, BeatPause);
                return true;
            case "pause":
                line.AddHold(position, ShortPause);
                return true;
        }

        const string pausePrefix = "pause=";
        if (key.StartsWith(pausePrefix))
        {
            float seconds;
            if (float.TryParse(key.Substring(pausePrefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            {
                line.AddHold(position, Mathf.Clamp(seconds, 0f, 8f));
                return true;
            }
        }

        // Not a marker we understand: leave the brackets in the line rather than eating the text.
        return false;
    }
}

/// <summary>One dialogue line, split into what the player reads and how it is typed.</summary>
public sealed class TypewriterLine
{
    /// <summary>The visible text, with the narration marker and every pacing marker removed.</summary>
    public string Body = string.Empty;

    /// <summary>True when the line was written as narration with a leading '~'.</summary>
    public bool Narration;

    /// <summary>True when the line carries its own mood, rather than using the typewriter defaults.</summary>
    public bool HasMood;

    /// <summary>Typing speed multiplier: 1 is the default pace, above 1 is faster.</summary>
    public float Speed = 1f;

    /// <summary>The hold after a full stop, question mark or exclamation mark, in seconds.</summary>
    public float SentencePause = TypewriterPacing.DefaultSentencePause;

    /// <summary>The hold after a comma, semicolon, colon or dash, in seconds.</summary>
    public float ClausePause = TypewriterPacing.DefaultClausePause;

    readonly List<int> holdPositions = new List<int>();
    readonly List<float> holdDurations = new List<float>();

    public void SetMood(float speed, float sentencePause, float clausePause)
    {
        Speed = Mathf.Max(0.1f, speed);
        SentencePause = Mathf.Max(0f, sentencePause);
        ClausePause = Mathf.Max(0f, clausePause);
        HasMood = true;
    }

    /// <summary>Adds a hold just before the character at <paramref name="position"/> in the body.</summary>
    public void AddHold(int position, float duration)
    {
        if (duration <= 0f)
            return;

        holdPositions.Add(position);
        holdDurations.Add(duration);
    }

    /// <summary>The total hold written before a position in the body, in seconds.</summary>
    public float HoldAt(int position)
    {
        float total = 0f;

        for (int i = 0; i < holdPositions.Count; i++)
        {
            if (holdPositions[i] == position)
                total += holdDurations[i];
        }

        return total;
    }
}

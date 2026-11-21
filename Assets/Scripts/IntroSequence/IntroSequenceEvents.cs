using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class IntroSequenceEvents : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject textBox;
    [SerializeField] GameObject mainTextObject;
    [SerializeField] GameObject nextButton;
    [SerializeField] GameObject charName;
    [SerializeField] GameObject fadeOut;

    [Header("Picture Stack")]
    [Tooltip("Picture1 lies over Picture2, over Picture3, over Picture4, so a fade only ever removes the top image and the next one is already waiting underneath.")]
    [SerializeField] GameObject picture1;
    [SerializeField] GameObject picture2;
    [SerializeField] GameObject picture3;
    [SerializeField] GameObject picture4;

    [Tooltip("How long each picture takes to fade in or out, in seconds.")]
    [SerializeField] float pictureFadeDuration = 1.5f;

    [Header("Text System")]
    [SerializeField] string textToSpeak;
    [SerializeField] float charDelay = 0.1f;   // typing speed (seconds per character)

    [Tooltip("Extra beat after a full stop, question mark or exclamation mark.")]
    [SerializeField] float sentencePause = 0.18f;

    [Tooltip("Extra beat after a comma, semicolon, colon or dash.")]
    [SerializeField] float clausePause = 0.08f;

    [Tooltip("How long each character takes to fade and rise into place, at normal speed.")]
    [SerializeField] float revealDuration = 0.16f;

    // Characters fade and rise into place as they are typed, the same way dialogue does.
    readonly TypewriterReveal reveal = new TypewriterReveal();

    int introIndex = 0;
    bool isPlayingLine = false;
    bool isSkipping = false;

    TMP_Text tmp;

    // One running fade per picture, so a beat can finish or replace the fade before it.
    readonly Dictionary<GameObject, Coroutine> pictureFades = new Dictionary<GameObject, Coroutine>();
    readonly Dictionary<GameObject, Graphic> pictureGraphics = new Dictionary<GameObject, Graphic>();

    void Start()
    {
        tmp = textBox.GetComponent<TMP_Text>();
        // The whole intro is the narrator speaking, with no nameplate, so it is all in italics.
        tmp.fontStyle = FontStyles.Italic;

        // The intro always opens on the scene's own background; the first line brings Picture1 up.
        HidePictureImmediately(picture1);
        HidePictureImmediately(picture2);
        HidePictureImmediately(picture3);
        HidePictureImmediately(picture4);

        StartCoroutine(EventStarter());
    }

    void LateUpdate()
    {
        // Typing itself is handled by PlayIntroLine; this only draws the characters fading in.
        if (reveal.IsAnimating)
            reveal.Animate(Time.time);
    }

    IEnumerator EventStarter()
    {
        yield return new WaitForSeconds(7f);

        mainTextObject.SetActive(true);
        textBox.SetActive(true);
        charName.GetComponent<TMP_Text>().text = "";

        introIndex = 0;
        yield return StartCoroutine(PlayIntroLine(introIndex));
    }

    IEnumerator PlayIntroLine(int index)
    {
        isPlayingLine = true;
        isSkipping = false;
        nextButton.SetActive(false);

        PlayPictureBeat(index);

        switch (index)
        {
            case 0:
                textToSpeak = "[slow]Light hides within the darkness of the people...";
                break;
            case 1:
                textToSpeak = "[slow]...rooted from the darkness of their own world.";
                break;
            case 2:
                textToSpeak = "[calm]To find light, you must first overcome the darkness.";
                break;
            case 3:
                textToSpeak = "[slow]Only then...";
                break;
            case 4:
                textToSpeak = "[soft]...the world will shine bright.";
                break;
            case 5:
                textToSpeak = "[calm]You know how I know? Perhaps not...";
                break;
            case 6:
                textToSpeak = "[calm]...but it is a fact that many already take for granted.";
                break;
            case 7:
                textToSpeak = "[slow]Darkness can consume you in the scarcity of light.";
                break;
            case 8:
                textToSpeak = "[slow]It's for this reason that you and I can have a world as bright as...";
                break;
            case 9:
                textToSpeak = "[tense]But it is not the outside world.[pause=0.6]";
                break;
            case 10:
                textToSpeak = "[slow]It is your world.";
                break;
            case 11:
                textToSpeak = "[soft]The one you, and only you, can perceive.";
                break;
            case 12:
                textToSpeak = "[calm]Darkness prevails in the world of the people, but light prevails in your world.";
                break;
            case 13:
                textToSpeak = "[slow]As hard as it may seem at first, the light is always there—omnipresent...";
                break;
            case 14:
                textToSpeak = "[soft]...waiting.[pause=0.9]";
                break;
            case 15:
                textToSpeak = "[tense]Remember, never confuse light for darkness...";
                break;
            case 16:
                textToSpeak = "[tense]...for it's the greatest deception of all...";
                break;
            case 17:
                textToSpeak = "[slow]...the greatest error that a human being can commit.";
                break;
            case 18:
                textToSpeak = "[calm]And you, my friend, are a human being.";
                break;
            case 19:
                textToSpeak = "[slow]A human being, and a human being alone, can find the light.";
                break;
                
        }

        // The line can carry pacing markers: a mood like [slow], and holds like [pause=0.8] between
        // words. They are stripped here, so only the words themselves are ever shown.
        TypewriterLine line = TypewriterPacing.Parse(textToSpeak);

        float speed = Mathf.Max(0.35f, line.Speed);
        float gap = charDelay / speed;

        // Lay the whole line out once, then uncover it a character at a time. TMP keeps the same
        // layout throughout, so the sentence never re-wraps or shifts while it is being typed, and
        // each character fades and rises into place rather than snapping on.
        tmp.text = line.Body;

        reveal.Duration = Mathf.Clamp(revealDuration / speed, 0.05f, 0.3f);
        reveal.SentencePause = line.HasMood ? line.SentencePause : sentencePause;
        reveal.ClausePause = line.HasMood ? line.ClausePause : clausePause;
        reveal.Begin(tmp);

        int totalCharacters = tmp.textInfo.characterCount;
        int shown = 0;
        float wait = 0f;

        // A hold written before the first word is a silence that opens the line.
        float leadHold = line.HoldAt(0);
        if (leadHold > 0f)
            yield return new WaitForSeconds(leadHold);

        // Typewriter coroutine: slower, controlled speed, with an even beat between characters
        // at any frame rate instead of one character per frame boundary.
        while (shown < totalCharacters)
        {
            if (isSkipping)
            {
                // Instantly show full line if user clicked during typing
                break;
            }

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

            reveal.Reveal(shown, Time.time);
            shown++;

            TMP_TextInfo info = tmp.textInfo;
            char character = info.characterInfo[shown - 1].character;
            char nextCharacter = shown < totalCharacters ? info.characterInfo[shown].character : '\0';
            wait = gap + reveal.PauseAfter(character, nextCharacter) + line.HoldAt(shown);

            yield return null;
        }

        // A hold at the end of a line is a silence before the line is finished.
        float tailHold = line.HoldAt(totalCharacters);
        if (tailHold > 0f && !isSkipping)
            yield return new WaitForSeconds(tailHold);

        // A skipped line is shown at once; a finished one lets its last characters keep fading in
        // through the short pause before Next appears.
        if (isSkipping)
            reveal.Finish();

        // Small pause after full line is visible
        yield return new WaitForSeconds(0.3f);

        isPlayingLine = false;
        nextButton.SetActive(true);
    }

    public void NextButton()
    {
        // If still typing → skip to full line
        if (isPlayingLine)
        {
            isSkipping = true;
            return;
        }

        introIndex++;

        if (introIndex <= 19)
        {
            StartCoroutine(PlayIntroLine(introIndex));
        }
        else
        {
            StartCoroutine(FadeOutAndLoadNext());
        }
    }

    // =====================================================
    // PICTURES
    // =====================================================

    /// <summary>
    /// The picture beats ride on top of the narration. Each one starts as its line begins typing, so
    /// the image changes under the words instead of after them.
    /// </summary>
    void PlayPictureBeat(int index)
    {
        switch (index)
        {
            case 0:
                // "Light hides within the darkness of the people..."
                FadePictureIn(picture1);
                break;
            case 1:
                // "...rooted from the darkness of their own world."
                FadePictureOut(picture1, picture2);
                break;
            case 2:
                // "To find light, you must first overcome the darkness."
                FadePictureOut(picture2, picture3);
                break;
            case 4:
                // "...the world will shine bright."
                FadePictureOut(picture3, picture4);
                break;
            case 7:
                // "Darkness can consume you in the scarcity of light."
                // Nothing is left under Picture4, so the last image gives way to the scene itself.
                FadePictureOut(picture4, null);
                break;
        }
    }

    void FadePictureIn(GameObject picture)
    {
        if (picture == null)
            return;

        SetPictureAlpha(picture, 0f);
        RunPictureFade(picture, 1f);
    }

    void FadePictureOut(GameObject picture, GameObject reveal)
    {
        if (picture == null)
            return;

        // If the player ran ahead of this picture's fade-in, finish it first: the image it uncovers
        // should come out from behind a fully opaque picture, not a half-faded one.
        FinishPictureFade(picture);

        if (reveal != null)
        {
            reveal.SetActive(true);
            SetPictureAlpha(reveal, 1f);
        }

        RunPictureFade(picture, 0f);
    }

    void RunPictureFade(GameObject picture, float target)
    {
        StopPictureFade(picture);
        pictureFades[picture] = StartCoroutine(FadePictureRoutine(picture, target));
    }

    /// <summary>Brings a picture to full strength straight away, cancelling any fade in progress.</summary>
    void FinishPictureFade(GameObject picture)
    {
        StopPictureFade(picture);
        SetPictureAlpha(picture, 1f);
        picture.SetActive(true);
    }

    void StopPictureFade(GameObject picture)
    {
        Coroutine running;
        if (pictureFades.TryGetValue(picture, out running) && running != null)
            StopCoroutine(running);

        pictureFades.Remove(picture);
    }

    IEnumerator FadePictureRoutine(GameObject picture, float target)
    {
        picture.SetActive(true);

        float start = PictureAlpha(picture);
        float elapsed = 0f;

        while (elapsed < pictureFadeDuration)
        {
            elapsed += Time.deltaTime;

            // SmoothStep at both ends, so the change eases out of the old image and into the new one.
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pictureFadeDuration));
            SetPictureAlpha(picture, Mathf.Lerp(start, target, eased));

            yield return null;
        }

        SetPictureAlpha(picture, target);

        if (target <= 0f)
            picture.SetActive(false);
    }

    void HidePictureImmediately(GameObject picture)
    {
        if (picture == null)
            return;

        SetPictureAlpha(picture, 0f);
        picture.SetActive(false);
    }

    Graphic PictureGraphic(GameObject picture)
    {
        Graphic graphic;
        if (pictureGraphics.TryGetValue(picture, out graphic) && graphic != null)
            return graphic;

        graphic = picture.GetComponent<Graphic>();
        pictureGraphics[picture] = graphic;
        return graphic;
    }

    float PictureAlpha(GameObject picture)
    {
        Graphic graphic = PictureGraphic(picture);
        return graphic != null ? graphic.color.a : 0f;
    }

    void SetPictureAlpha(GameObject picture, float alpha)
    {
        if (picture == null)
            return;

        Graphic graphic = PictureGraphic(picture);
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    IEnumerator FadeOutAndLoadNext()
    {
        nextButton.SetActive(false);

        if (fadeOut != null)
        {
            fadeOut.SetActive(true);
        }

        // Let the dialogue box play its own exit while the screen begins to darken.
        yield return new WaitForSeconds(0.35f);

        textBox.SetActive(false);
        mainTextObject.SetActive(false);

        yield return new WaitForSeconds(5.65f);

        // The intro hands over to whichever scene follows it in the build list, so moving or
        // inserting a scene in Build Settings moves the hand-off with it.
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;

        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            yield return SceneManager.LoadSceneAsync(nextIndex);
        }
        else
        {
            Debug.LogWarning(
                $"IntroSequence is the last scene in the build list (index {currentIndex}), so there is nothing to load next.");
        }
    }
}
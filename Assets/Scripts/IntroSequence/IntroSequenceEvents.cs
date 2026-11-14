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

        // The intro always opens on the scene's own background; the first line brings Picture1 up.
        HidePictureImmediately(picture1);
        HidePictureImmediately(picture2);
        HidePictureImmediately(picture3);
        HidePictureImmediately(picture4);

        StartCoroutine(EventStarter());
    }

    void Update()
    {
        // No typing here anymore — typing is handled by a coroutine.
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
                textToSpeak = "Light hides within the darkness of the people...";
                break;
            case 1:
                textToSpeak = "...rooted from the darkness of their own world.";
                break;
            case 2:
                textToSpeak = "To find light, you must first overcome the darkness.";
                break;
            case 3:
                textToSpeak = "Only then...";
                break;
            case 4:
                textToSpeak = "...the world will shine bright.";
                break;
            case 5:
                textToSpeak = "You know how I know? Perhaps not...";
                break;
            case 6:
                textToSpeak = "...but it is a fact that already many take for granted.";
                break;
            case 7:
                textToSpeak = "Darkness can consume you in the scarcity of light.";
                break;
            case 8:
                textToSpeak = "For it's the latter reason that you and I can have a world as bright as...";
                break;
            case 9:
                textToSpeak = "But it is not the outside world.";
                break;
            case 10:
                textToSpeak = "It is your world.";
                break;
            case 11:
                textToSpeak = "The one you, and only you, can perceive.";
                break;
            case 12:
                textToSpeak = "Darkness prevails in the world of the people, but light prevails in your world.";
                break;
            case 13:
                textToSpeak = "As hard as it may seem at first, the light is always there—omnipresent...";
                break;
            case 14:
                textToSpeak = "...waiting.";
                break;
            case 15:
                textToSpeak = "Remember, never confuse light for darkness...";
                break;
            case 16:
                textToSpeak = "...for it's the greatest deception of all...";
                break;
            case 17:
                textToSpeak = "...the greatest error that a human being can commit.";
                break;
            case 18:
                textToSpeak = "And you, my friend, are a human being.";
                break;
            case 19:
                textToSpeak = "A human being, and a human being alone, can find the light.";
                break;
                
        }

        tmp.text = "";

        // Typewriter coroutine: slower, controlled speed
        for (int i = 0; i <= textToSpeak.Length; i++)
        {
            if (isSkipping)
            {
                // Instantly show full line if user clicked during typing
                tmp.text = textToSpeak;
                break;
            }

            tmp.text = textToSpeak.Substring(0, i);
            yield return new WaitForSeconds(charDelay);
        }

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

        if (introIndex <= 18)
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
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TheHiddenColony01 : MonoBehaviour
{
    [Header("Character Expressions")]
    public GameObject charNeutral;
    public GameObject charSurprised;
    public GameObject charHappy;
    public GameObject charEmbarrassed;
    public GameObject charConfused;
    public GameObject charSad;
    public GameObject charExcited;

    [Header("UI")]
    public GameObject fadeScreenIn;
    public GameObject textBox;
    [SerializeField] string textToSpeak;
    [SerializeField] int currentTextLength;
    [SerializeField] int textLength;
    [SerializeField] GameObject mainTextObject;
    [SerializeField] GameObject nextButton;
    [SerializeField] int eventPos = 0;
    [SerializeField] GameObject charName;
    [SerializeField] GameObject fadeOut;
    public GameObject Picture1;
    public GameObject Picture2;

    int introIndex = 0;
    bool isTyping = false; // Prevents spamming Next

    // The last intro line ("[When everyone went back to their daily routes, Bay began to approach the person.]")
    const int LastIntroIndex = 40;

    const int Picture1FadeIndex = 10;
    const int Picture2FadeIndex = 37;

    void Update()
    {
        textLength = TextCreator.charCount;
    }

    void Start()
    {
        // Character expressions are disabled: none of them are ever enabled
        HideAllExpressions();

        StartCoroutine(EventStarter());
        nextButton.SetActive(false); // Ensure it's hidden at start
    }

    void HideAllExpressions()
    {
        charNeutral.SetActive(false);
        charSurprised.SetActive(false);
        charHappy.SetActive(false);
        charEmbarrassed.SetActive(false);
        charConfused.SetActive(false);
        charSad.SetActive(false);
        charExcited.SetActive(false);
    }

    IEnumerator EventStarter()
    {
        yield return new WaitForSeconds(2f);
        fadeScreenIn.SetActive(false);
        yield return new WaitForSeconds(2f);
        mainTextObject.SetActive(true);
        textBox.SetActive(true);
        charName.GetComponent<TMP_Text>().text = "";

        introIndex = 0;
        yield return StartCoroutine(PlayIntroLine(introIndex));
        eventPos = 0;
    }

    IEnumerator PlayIntroLine(int index)
    {
        nextButton.SetActive(false);

        isTyping = true;
        switch (index)
        {
            case 0:
                textToSpeak = "[Just as Agnes said, there were a lot of new faces.]";
                break;
            case 1:
                textToSpeak = "[As he followed her, he locked eyes with everyone.]";
                break;
            case 2:
                textToSpeak = "[None gave off any malice, only mild curiosity when their eyes meet Bay's.]";
                break;
            case 3:
                textToSpeak = "[There was a long table centered on the room, with some scratches.]";
                break;
            case 4:
                textToSpeak = "[On the side walls, there are bulletin boards and corkboards with pinned papers...]";
                break;
            case 5:
                textToSpeak = "[...waiting for anyone who approached.]";
                break;
            case 6:
                textToSpeak = "[There were six computers on the far end wall next to the door...]";
                break;
            case 7:
                textToSpeak = "[...one of them being used by a hooded individual.]";
                break;
            case 8:
                textToSpeak = "[The room smelled of stale air from the amount of dust floating in the air.]";
                break;
            case 9:
                textToSpeak = "[He finally stopped when Agnes did, and stood before everyone.]";
                break;
            case 10:
                textToSpeak = "[There must be around eighteen teenagers his age.]";
                break;
            case 11:
                textToSpeak = "[Some older, some younger.]";
                break;
            case 12:
                textToSpeak = "[Agnes began speaking:]";
                break;
            case 13:
                textToSpeak = "Hey y'all, I'm back!";
                break;
            case 14:
                textToSpeak = "And look who I found...";
                break;
            case 15:
                textToSpeak = "...I knew we have to get him when I heard the local news.";
                break;
            case 16:
                textToSpeak = "Luckily for us, I was able to get a hold of him before they got him.";
                break;
            case 17:
                textToSpeak = "[Chatter and cheers exploded instantly when Agnes finished her sentence.]";
                break;
            case 18:
                textToSpeak = "[After a few seconds, she continued:]";
                break;
            case 19:
                textToSpeak = "As you may already know, the DCA recently increased the amount of DCP officers roaming around.";
                break;
            case 20:
                textToSpeak = "When they heard about Bay, they started to actively look for him.";
                break;
            case 21:
                textToSpeak = "Again, we're lucky to have him here...";
                break;
            case 22:
                textToSpeak = "...and he's the reason we can finally kick off our plan.";
                break;
            case 23:
                textToSpeak = "The Research and Technology Committee will be tracking down DCP officers and their whereabouts.";
                break;
            case 24:
                textToSpeak = "We need to get ready for this, we really do.";
                break;
            case 25:
                textToSpeak = "[She said the last part almost to herself.]";
                break;
            case 26:
                textToSpeak = "[Everyone started talking at all once.]";
                break;
            case 27:
                textToSpeak = "[Bay turned his head back at the hooded individual, who was now typing something on the keyboard.]";
                break;
            case 28:
                textToSpeak = "[His curiosity peeked, who was that?]";
                break;
            case 29:
                textToSpeak = "[Agnes turned to Bay, then began speaking with a smile on her face:]";
                break;
            case 30:
                textToSpeak = "Hey, let me get you acquainted with everyone. Let's go!";
                break;
            case 31:
                textToSpeak = "[Her voice was off, but he didn't question it.]";
                break;
            case 32:
                textToSpeak = "[Everyone was already coming at him all at once.]";
                break;
            case 33:
                textToSpeak = "[Everyone was getting their turn to greet and welcome Bay into The Hidden Colony.]";
                break;
            case 34:
                textToSpeak = "[He smiled through all of this, he didn't recall this feeling.";
                break;
            case 35:
                textToSpeak = "[But he knew that it was stupid to overthink everything right now.]";
                break;
            case 36:
                textToSpeak = "[He had a place where he was welcome.]";
                break;
            case 37:
                textToSpeak = "[He glanced between the heads of the people.]";
                break;
            case 38:
                textToSpeak = "[The same hooded individual had stood up to stretch.]";
                break;
            case 39:
                textToSpeak = "[They were wearing a long oversized hoodie and black baggy shorts.]";
                break;
            case 40:
                textToSpeak = "[When everyone went back to their daily routes, Bay began to approach the person.]";
                break;

        }

        charName.GetComponent<TMP_Text>().text = textToSpeak.StartsWith("[") ? "" : "Agnes";
        textBox.GetComponent<TMP_Text>().text = textToSpeak;
        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);

        if (index == Picture1FadeIndex)
        {
            yield return StartCoroutine(FadePicture(Picture1, false));
        }
        else if (index == Picture2FadeIndex)
        {
            yield return StartCoroutine(FadePicture(Picture2, false));
        }

        nextButton.SetActive(true);
        isTyping = false;
    }

    // NextButton to be called by Unity UI OnClick
    public void NextButton()
    {
        if (isTyping) return; // Don't skip the typing animation

        introIndex++;
        if (introIndex <= LastIntroIndex)
        {
            StartCoroutine(PlayIntroLine(introIndex));
        }
        else
        {
            StartCoroutine(EventFour());
        }
    }

    // =====================================================
    // HELPERS
    // =====================================================

    IEnumerator FadePicture(GameObject picture, bool fadeIn)
    {
        if (picture == null)
            yield break;

        CanvasGroup canvasGroup = picture.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = picture.AddComponent<CanvasGroup>();

        if (fadeIn)
        {
            picture.SetActive(true);
            canvasGroup.alpha = 0f;
        }

        float startAlpha = fadeIn ? 0f : canvasGroup.alpha;
        float fadeDuration = 1f;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;

            float targetAlpha = fadeIn ? 1f : 0f;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);

            yield return null;
        }

        canvasGroup.alpha = fadeIn ? 1f : 0f;
        if (!fadeIn)
            picture.SetActive(false);
    }

    IEnumerator EventFour()
    {
        nextButton.SetActive(false);
        textBox.SetActive(true);
        fadeOut.SetActive(true);
        yield return new WaitForSeconds(4f);
        SceneManager.LoadScene(13);
    }
}
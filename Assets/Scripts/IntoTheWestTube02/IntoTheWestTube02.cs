using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class IntoTheWestTube02 : MonoBehaviour
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

    // The last intro line ("[Without twinking it twice, she twisted it and it swung open.]")
    const int LastIntroIndex = 10;

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
                textToSpeak = "[It wasn't long before they reached the end of the narrow passage...]";
                break;
            case 1:
                textToSpeak = "[...where they were met with a undusted grey door.]";
                break;
            case 2:
                textToSpeak = "[Bay noticed the handwritten text on the door:]";
                break;
            case 3:
                textToSpeak = "[\"The Hidden Colony\"]";
                break;
            case 4:
                textToSpeak = "[After what seemed like ages, Agnes finally broke the silence:]";
                break;
            case 5:
                textToSpeak = "Okay, here we are. The Hidden Colony.";
                break;
            case 6:
                textToSpeak = "All of us are Domeborn, we were born here in the Dome.";
                break;
            case 7:
                textToSpeak = "Come on, don't be too scared with the new faces.";
                break;
            case 8:
                textToSpeak = "I'm sure you'll like it, I'll tell you more later.";
                break;
            case 9:
                textToSpeak = "[Agnes stepped in front of him. and grabbed the handle.]";
                break;
            case 10:
                textToSpeak = "[Without twinking it twice, she twisted it and it swung open.]";
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

        // On this line Picture1 fades out. Picture2 is left untouched.
        if (textToSpeak == "Okay, here we are. The Hidden Colony.")
        {
            yield return StartCoroutine(FadePicture(Picture1, false));
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
        SceneManager.LoadScene(12);
    }
}

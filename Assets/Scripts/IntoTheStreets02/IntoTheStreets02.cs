using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class IntoTheStreets02 : MonoBehaviour
{
    [Header("Character Expressions")]
    public GameObject charNeutral;
    public GameObject charSurprised;
    public GameObject charHappy;
    public GameObject charEmbarrassed;
    public GameObject charConfused;
    public GameObject charSad;
    public GameObject charExcited;

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

    int introIndex = 0;
    bool isTyping = false; // Prevents spamming Next

    void Update()
    {
        textLength = TextCreator.charCount;
    }

    void Start()
    {
        StartCoroutine(EventStarter());
        nextButton.SetActive(false); // Ensure it's hidden at start
    }

    // Expression switching (blend, pop, hide) lives in ExpressionSet
    ExpressionSet expressionSet;

    void Awake()
    {
        expressionSet = new ExpressionSet(this, GetExpressionObjects());
    }

    GameObject[] GetExpressionObjects()
    {
        return new GameObject[]
        {
            charNeutral, charSurprised, charHappy, charEmbarrassed,
            charConfused, charSad, charExcited
        };
    }

    void HideAllExpressions()
    {
        expressionSet.HideAll();
    }

    void ShowExpression(string expression)
    {
        GameObject target = null;

        switch (expression)
        {
            case "neutral":     target = charNeutral; break;
            case "surprised":   target = charSurprised; break;
            case "happy":       target = charHappy; break;
            case "smile":       target = charHappy; break;
            case "embarrassed": target = charEmbarrassed; break;
            case "confused":    target = charConfused; break;
            case "sad":         target = charSad; break;
            case "excited":     target = charExcited; break;
        }

        // An unknown expression leaves nobody on screen, same as before
        if (target == null)
        {
            expressionSet.HideAll();
            return;
        }

        expressionSet.Show(target);
    }

    IEnumerator EventStarter()
    {
        yield return new WaitForSeconds(2f);
        fadeScreenIn.SetActive(false);
        ShowExpression("neutral");
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
                ShowExpression("smile");
                textToSpeak = "~A young girl, with long light blonde hair and blue, crystalline eyes.";
                break;
            case 1:
                ShowExpression("smile");
                textToSpeak = "~She wears a black full-body suit, loose enough to let her body breathe.";
                break;
            case 2:
                ShowExpression("smile");
                textToSpeak = "~The girl looked up at Bay, who was still looking at her.";
                break;
            case 3:
                ShowExpression("smile");
                textToSpeak = "~The girl opened her mouth to speak:";
                break;
            case 4:
                ShowExpression("happy");
                textToSpeak = "Hey there, nice Transpod I got, right?";
                break;
            case 5:
                ShowExpression("happy");
                textToSpeak = "~The girl chuckled, then returned her gaze to Bay.";
                break;
            case 6:
                ShowExpression("confused");
                textToSpeak = "Wait, aren't you that boy who was unconscious in the plaza?";
                break;
            case 7:
                ShowExpression("happy");
                textToSpeak = "I guess you must not remember, but you had a pretty rough fall!";
                break;
            case 8:
                ShowExpression("happy");
                textToSpeak = "You're probably crazy strong for sticking the landing though, I can barely go around without getting a scratch.";
                break;
            case 9:
                ShowExpression("smile");
                textToSpeak = "~Bay looked puzzled, confused. He looked at the vehicle behind her. Transpod? He tried to recall if he had ever heard that word.";
                break;
            case 10:
                ShowExpression("neutral");
                textToSpeak = "~But nothing came to him.";
                break;
            case 11:
                ShowExpression("happy");
                textToSpeak = "You're pretty quiet. Man of few words, huh?";
                break;
            case 12:
                ShowExpression("smile");
                textToSpeak = "Well, it's not like I just invaded your personal space.";
                break;
            case 13:
                ShowExpression("happy");
                textToSpeak = "~The girl said sarcastically, letting out a short giggle.";
                break;
            case 14:
                ShowExpression("neutral");
                textToSpeak = "You don't look like you're from around these streets.";
                break;
            case 15:
                ShowExpression("neutral");
                textToSpeak = "You probably came from The Surface, right?";
                break;
            case 16:
                ShowExpression("surprised");
                textToSpeak = "~As Bay started to look even more puzzled, the girl finally came to her senses.";
                break;
            case 17:
                ShowExpression("embarrassed");
                textToSpeak = "Oh, sorry. I haven't even introduced myself!";
                break;
            case 18:
                ShowExpression("happy");
                textToSpeak = "My name is Agnes Blackwood, nice to meet you!";
                break;
            case 19:
                ShowExpression("happy");
                textToSpeak = "What's your name?";
                break;
            case 20:
                ShowExpression("smile");
                textToSpeak = "~His name. That was one of the things he could actually recall, so he let it out:";
                break;
            case 21:
                ShowExpression("surprised");
                textToSpeak = "Bay. Bay Ausman.";
                break;
            case 22:
                ShowExpression("smile");
                textToSpeak = "~Agnes looked at Bay, smiling.";
                break;
            case 23:
                ShowExpression("happy");
                textToSpeak = "Bay, huh? Fancy name. I like it.";
                break;
            case 24:
                ShowExpression("embarrassed");
                textToSpeak = "~There were a few seconds of awkward silence before Agnes spoke again.";
                break;
            case 25:
                ShowExpression("excited");
                textToSpeak = "Hey, you wanna check out my Transpod? I decorated it with a bunch of stuff I have.";
                break;
            case 26:
                ShowExpression("excited");
                textToSpeak = "Come on, I'll even give you a free ride!";
                break;
            case 27:
                ShowExpression("happy");
                textToSpeak = "~She didn't finish her sentence when she began to walk towards the Transpod's entrance.";
                break;
            case 28:
                ShowExpression("happy");
                textToSpeak = "~Bay had no choice but to follow her, and he did.";
                break;

        }

        // Bay's own line ("Bay. Bay Ausman.") is spoken by the player character.
        bool narration = DialogueText.IsNarration(textToSpeak);
        string body = DialogueText.Body(textToSpeak);
        string speaker = narration ? "" : (index == 21 ? "You" : "Agnes");
        charName.GetComponent<TMP_Text>().text = speaker;
        textBox.GetComponent<TMP_Text>().text = body;
        DialogueText.ApplyStyle(textBox.GetComponent<TMP_Text>(), DialogueText.ShouldUseItalics(textToSpeak, speaker));
        currentTextLength = body.Length;
        TextCreator.Run(textToSpeak);

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);
        nextButton.SetActive(true);
        isTyping = false;
    }

    // NextButton to be called by Unity UI OnClick
    public void NextButton()
    {
        if (isTyping) return; // Don't skip the typing animation

        introIndex++;
        if (introIndex <= 28)
        {
            StartCoroutine(PlayIntroLine(introIndex));
        }
        else
        {
            StartCoroutine(EventFour());
        }
    }

    IEnumerator EventFour()
    {
        nextButton.SetActive(false);
        ShowExpression("happy");
        textBox.SetActive(true);
        fadeOut.SetActive(true);
        yield return new WaitForSeconds(4f);
        yield return SceneManager.LoadSceneAsync(8);
    }
}

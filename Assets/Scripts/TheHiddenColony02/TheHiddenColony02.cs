using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// NOTE: Unity needs a MonoBehaviour's file name to match its class name.
// If the inspector refuses it, rename this file to WestTubeV2.cs.
public class TheHiddenColony02 : MonoBehaviour
{
    [Header("Character Expressions")]
    public GameObject charNeutral;
    public GameObject charSurprised;
    public GameObject charHappy;
    public GameObject charEmbarrassed;
    public GameObject charConfused;
    public GameObject charSad;
    public GameObject charExcited;
    public GameObject charMasked;

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

    [Header("Scene Pictures")]
    public GameObject Picture1;
    public GameObject Picture2;
    public GameObject Picture3;

    [Header("Choice Buttons")]
    public GameObject ChoiceButton1;
    public GameObject ChoiceButton2;
    public GameObject ChoiceButton3;

    int introIndex = 0;
    bool isTyping = false; // Prevents spamming Next
    bool expressionsHiddenUntilTunnelEntrance = false;

    // Prevents the player from pressing buttons while a choice dialogue is running
    bool choiceDialoguePlaying = false;

    // Used to make choice dialogue wait for the player to click Next
    bool waitingForChoiceNext = false;

    void Update()
    {
        textLength = TextCreator.charCount;
    }

    void Start()
    {
        // Hide the choice buttons at the beginning
        if (ChoiceButton1 != null)
            ChoiceButton1.SetActive(false);

        if (ChoiceButton2 != null)
            ChoiceButton2.SetActive(false);

        if (ChoiceButton3 != null)
            ChoiceButton3.SetActive(false);

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
        charMasked.SetActive(false);
    }

    void ShowExpression(string expression)
    {
        HideAllExpressions();

        if (expressionsHiddenUntilTunnelEntrance)
            return;

        switch (expression)
        {
            case "neutral":     charNeutral.SetActive(true); break;
            case "surprised":   charSurprised.SetActive(true); break;
            case "happy":       charHappy.SetActive(true); break;
            case "smile":       charHappy.SetActive(true); break;
            case "embarrassed": charEmbarrassed.SetActive(true); break;
            case "confused":    charConfused.SetActive(true); break;
            case "sad":         charSad.SetActive(true); break;
            case "excited":     charExcited.SetActive(true); break;
            case "masked":      charMasked.SetActive(true); break;
        }
    }

    void FadeOutActiveExpressions()
    {
        GameObject[] expressions =
        {
            charNeutral, charSurprised, charHappy, charEmbarrassed,
            charConfused, charSad, charExcited, charMasked
        };

        foreach (GameObject expression in expressions)
        {
            if (expression != null && expression.activeSelf)
                StartCoroutine(FadeAndDisable(expression));
        }
    }

    IEnumerator FadeAndDisable(GameObject target)
    {
        if (target == null)
            yield break;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        float startAlpha = canvasGroup.alpha;
        const float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        target.SetActive(false);
    }

    IEnumerator FadeInObject(GameObject target)
    {
        if (target == null)
            yield break;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        target.SetActive(true);
        canvasGroup.alpha = 0f;

        const float duration = 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
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

        // Make sure the choices are hidden while normal dialogue is playing
        if (ChoiceButton1 != null)
            ChoiceButton1.SetActive(false);

        if (ChoiceButton2 != null)
            ChoiceButton2.SetActive(false);

        if (ChoiceButton3 != null)
            ChoiceButton3.SetActive(false);

        isTyping = true;
        switch (index)
        {
            case 0:
                ShowExpression("smile");
                textToSpeak = "[Bay began to approach the individual, who was inside their own world.]";
                break;
            case 1:
                ShowExpression("smile");
                textToSpeak = "[When he got close enough, he noticed the person was making some kind of poster.]";
                break;
            case 2:
                ShowExpression("smile");
                textToSpeak = "[He managed to read it once he was close enough...]";
                break;
            case 3:
                ShowExpression("smile");
                textToSpeak = "[...his eyes widened, something between confusion and astonishment washed over him.]";
                break;
            case 4:
                ShowExpression("embarrassed");
                textToSpeak = "THE SWEEPERS ARE FAR FROM RIGHT.";
                break;
            case 5:
                ShowExpression("happy");
                textToSpeak = "LET OUS OUTSIDE, THEY'RE HIDING THE WORLD FROM US.";
                break;
            case 6:
                ShowExpression("confused");
                textToSpeak = "DON'T LET US ROT HERE.";
                break;
            case 7:
                ShowExpression("happy");
                textToSpeak = "WE ARE IMPORTANT. WE ARE HUMANS.";
                break;
            case 8:
                ShowExpression("smile");
                expressionsHiddenUntilTunnelEntrance = true;
                StartCoroutine(FadeAndDisable(Picture1));
                FadeOutActiveExpressions();
                textToSpeak = "[When Bay got close enough, the individual turned abruptly from their chair...]";
                break;
            case 9:
                ShowExpression("smile");
                textToSpeak = "[...he finally saw their face.]";
                break;
            case 10:
                ShowExpression("smile");
                StartCoroutine(FadeAndDisable(Picture2));
                textToSpeak = "[It was a girl, maybe fifteen years old or so.]";
                break;
            case 11:
                ShowExpression("smile");
                textToSpeak = "[She took off her hood, revealing her long blonde hair cascading down her back.]";
                break;
            case 12:
                ShowExpression("smile");
                textToSpeak = "[She looked familiar, but he couldn't quite put his finger on where he saw her before.]";
                break;
            case 13:
                ShowExpression("smile");
                textToSpeak = "[She was wearing headphones, which she didn't take off before she completely faced him.]";
                break;
            case 14:
                ShowExpression("smile");
                textToSpeak = "[She began speaking:]";
                break;
            case 15:
                ShowExpression("smile");
                textToSpeak = "Oh, you must be the new Dommie. Bay, ain't it?";
                break;
            case 16:
                ShowExpression("smile");
                textToSpeak = "Nice to meet ya, name's Morgana.";
                break;
            case 17:
                ShowExpression("embarrassed");
                textToSpeak = "I'm the Chairperson of the Research & Technology Committee.";
                break;
            case 18:
                ShowExpression("happy");
                textToSpeak = "Right now I'm making a poster, we'll print it and then spread them around like crazy 'round the Dome.";
                break;
            case 19:
                ShowExpression("happy");
                textToSpeak = "Luckily, they ain't know where we are. But they'll soon do if we keep at it.";
                break;
            case 20:
                ShowExpression("happy");
                textToSpeak = "[She gave him a knowing grin.]";
                break;
            case 21:
                ShowExpression("happy");
                textToSpeak = "But now that you're here, Agnes will finally put ya to work with us.";
                break;
            case 22:
                ShowExpression("smile");
                textToSpeak = "She'll put ya somewhere you'll fit nicely, don't worry. She's the bossy around here anyways.";
                break;
            case 23:
                ShowExpression("smile");
                textToSpeak = "We are The Underdomers, and this is our home...";
                break;
            case 24:
                ShowExpression("smile");
                textToSpeak = "...The Hidden Colony.";
                break;
            case 25:
                expressionsHiddenUntilTunnelEntrance = false;
                ShowExpression("smile");
                StartCoroutine(FadeAndDisable(Picture3));
                StartCoroutine(FadeInObject(charHappy));
                textToSpeak = "We know what that Dome Control Administration group is doing, I reckon you already know a bit?";
                break;
            case 26:
                ShowExpression("happy");
                textToSpeak = "They don't want anyone knowing 'bout what's up there, outside...";
                break;
            case 27:
                ShowExpression("happy");
                textToSpeak = "...what they call The Surface.";
                break;
            case 28:
                ShowExpression("happy");
                textToSpeak = "[Agnes overheard the conversation and was already between Bay and Morgana.]";
                break;
            case 29:
                ShowExpression("happy");
                textToSpeak = "[She gave her a look, then she faced Bay.]";
                break;
            case 30:
                ShowExpression("happy");
                textToSpeak = "Oh, it seems she introduced herself to you first.";
                break;
            case 31:
                ShowExpression("happy");
                textToSpeak = "She's Morgana, our tech-savvy member and chairperson of the Research & Technology Committee.";
                break;
            case 32:
                ShowExpression("happy");
                textToSpeak = "She was the one who told me where you were. Right near the clinic.";
                break;
            case 33:
                ShowExpression("embarrassed");
                textToSpeak = "You see this thing here?";
                break;
            case 34:
                ShowExpression("embarrassed");
                textToSpeak = "Agnes turned herself sideways, and right off the bat...";
                break;
            case 35:
                ShowExpression("embarrassed");
                textToSpeak = "...Bay noticed the strange device she was wearing on her ear.";
                break;

        }

        string speaker = textToSpeak.StartsWith("[")
            ? ""
            : index == 32 || index == 38 || index == 40 ? "Ryder" : "Agnes";

        charName.GetComponent<TMP_Text>().text = speaker;

        if (speaker == "Ryder")
            ShowExpression("masked");
        textBox.GetComponent<TMP_Text>().text = textToSpeak;
        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);

        // -------------------------------------------------
        // CHOICE POINT (after Case 41)
        // -------------------------------------------------

        if (index == 41)
        {
            // Hide the normal next button
            nextButton.SetActive(false);

            // Show all three choices
            if (ChoiceButton1 != null)
                ChoiceButton1.SetActive(true);

            if (ChoiceButton2 != null)
                ChoiceButton2.SetActive(true);

            if (ChoiceButton3 != null)
                ChoiceButton3.SetActive(true);

            isTyping = false;
            yield break;
        }

        nextButton.SetActive(true);
        isTyping = false;
    }

    // NextButton to be called by Unity UI OnClick
    public void NextButton()
    {
        // -------------------------------------------------
        // CHOICE DIALOGUE
        // -------------------------------------------------

        // If a choice dialogue is currently playing,
        // Next should ONLY be used to advance the current
        // choice line.
        if (choiceDialoguePlaying)
        {
            if (waitingForChoiceNext)
            {
                // Hide the button immediately after clicking it
                nextButton.SetActive(false);

                // Allow PlayChoiceLine() to continue
                waitingForChoiceNext = false;
            }

            return;
        }

        // -------------------------------------------------
        // INTRO SEQUENCE
        // -------------------------------------------------

        if (isTyping) return; // Don't skip the typing animation

        introIndex++;
        if (introIndex <= 41)
        {
            StartCoroutine(PlayIntroLine(introIndex));
        }
        else
        {
            StartCoroutine(EventFour());
        }
    }

    // =====================================================
    // CHOICE 1
    // =====================================================

    public void ChoiceButton1Pressed()
    {
        if (choiceDialoguePlaying)
            return;

        StartCoroutine(PlayChoice1());
    }

    IEnumerator PlayChoice1()
    {
        choiceDialoguePlaying = true;
        HideChoiceButtons();

        DialogueLine[] choice1Dialogue =
        {
            new DialogueLine("", "[Ryder looked at Bay, then slightly nodded.]"),
            new DialogueLine("Ryder", "That's what I wanted to hear, carry on.")
        };

        yield return StartCoroutine(PlayDialogueLines(choice1Dialogue));
        yield return StartCoroutine(PlayDialogueLines(aftermathDialogue));

        FinishChoice();
    }

    // =====================================================
    // CHOICE 2
    // =====================================================

    public void ChoiceButton2Pressed()
    {
        if (choiceDialoguePlaying)
            return;

        StartCoroutine(PlayChoice2());
    }

    IEnumerator PlayChoice2()
    {
        choiceDialoguePlaying = true;
        HideChoiceButtons();

        DialogueLine[] choice2Dialogue =
        {
            new DialogueLine("", "[Ryder looked at Bay, slightly nodding at his response.]")
        };

        yield return StartCoroutine(PlayDialogueLines(choice2Dialogue));
        yield return StartCoroutine(PlayDialogueLines(aftermathDialogue));

        FinishChoice();
    }

    // =====================================================
    // CHOICE 3
    // =====================================================

    public void ChoiceButton3Pressed()
    {
        if (choiceDialoguePlaying)
            return;

        StartCoroutine(PlayChoice3());
    }

    IEnumerator PlayChoice3()
    {
        choiceDialoguePlaying = true;
        HideChoiceButtons();

        DialogueLine[] choice3Dialogue =
        {
            new DialogueLine("", "[Ryder looked at Bay, he was being directly challenged.]"),
            new DialogueLine("", "[He let out a small laugh through the mask, coming out as a distorted mess.]"),
            new DialogueLine("Ryder", "You've got guts, boy."),
            new DialogueLine("Ryder", "You look like a good person, but the moment you act up..."),
            new DialogueLine("Ryder", "...my fist will end up permanently marked on your cheek.")
        };

        yield return StartCoroutine(PlayDialogueLines(choice3Dialogue));
        yield return StartCoroutine(PlayDialogueLines(aftermathDialogue));

        FinishChoice();
    }

    // =====================================================
    // DIALOGUE THAT PLAYS AFTER ANY OF THE THREE CHOICES
    // =====================================================

    DialogueLine[] aftermathDialogue =
    {
        new DialogueLine("", "[Agnes' eyes widened.]"),
        new DialogueLine("Agnes", "Don't be too harsh with him, he's just... different.", "embarrassed"),
        new DialogueLine("", "[Ryder stepped out of their way, letting them continue.]"),
        new DialogueLine("Ryder", "Whatever, just don't do anything dumb that might get me in trouble."),
        new DialogueLine("Ryder", "I still got to fill my monthly quota."),
        new DialogueLine("Ryder", "Have you with your... new recruit."),
        new DialogueLine("", "[Bay noticed the change in his tone, and what did he mean new recruit?]"),
        new DialogueLine("", "[Agnes started walking with purpose towards the tube, Bay following behind.]"),
        new DialogueLine("", "[Ryder looked back at the two before standing back at the entrance of the tube.]"),
        new DialogueLine("Ryder", "Come on, let's go..."),
        new DialogueLine("Ryder", "...or else we're gonna be late.")
    };

    // =====================================================
    // HELPERS
    // =====================================================

    void HideChoiceButtons()
    {
        if (ChoiceButton1 != null)
            ChoiceButton1.SetActive(false);

        if (ChoiceButton2 != null)
            ChoiceButton2.SetActive(false);

        if (ChoiceButton3 != null)
            ChoiceButton3.SetActive(false);

        nextButton.SetActive(false);
    }

    // Plays a list of dialogue lines one at a time.
    // Each line waits for the player to click Next.
    IEnumerator PlayDialogueLines(DialogueLine[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            yield return StartCoroutine(PlayChoiceLine(lines[i]));
        }
    }

    IEnumerator PlayChoiceLine(DialogueLine dialogueLine)
    {
        // Hide Next while the new line is typing
        nextButton.SetActive(false);

        // Make sure the waiting state is reset
        waitingForChoiceNext = false;

        // Set dialogue
        textToSpeak = dialogueLine.text;

        // Narration (lines wrapped in brackets) has no character name
        charName.GetComponent<TMP_Text>().text = textToSpeak.StartsWith("[") ? "" : dialogueLine.speaker;

        if (dialogueLine.speaker == "Ryder")
            ShowExpression("masked");
        else if (!string.IsNullOrEmpty(dialogueLine.expression))
            ShowExpression(dialogueLine.expression);

        textBox.GetComponent<TMP_Text>().text = textToSpeak;

        currentTextLength = textToSpeak.Length;

        // Reset typewriter counter
        TextCreator.charCount = 0;

        // Start typewriter
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);

        // Wait until the entire line has finished typing
        yield return new WaitUntil(() => textLength >= currentTextLength);

        // The line is completely typed.
        // Show Next so the player can manually continue.
        nextButton.SetActive(true);

        // Tell NextButton() that we are waiting for a click.
        waitingForChoiceNext = true;

        // IMPORTANT:
        // Do NOT continue automatically.
        // Wait here until the player presses Next.
        yield return new WaitUntil(() => waitingForChoiceNext == false);
    }

    void FinishChoice()
    {
        choiceDialoguePlaying = false;

        // Continue to next event
        eventPos = 4;
        StartCoroutine(EventFour());
    }

    IEnumerator EventFour()
    {
        nextButton.SetActive(false);
        ShowExpression("happy");
        textBox.SetActive(true);
        fadeOut.SetActive(true);
        yield return new WaitForSeconds(4f);
        yield return SceneManager.LoadSceneAsync(10);
    }

    // A single line of dialogue: who is speaking (empty for narration)
    // and the text to display.
    class DialogueLine
    {
        public string speaker;
        public string text;
        public string expression;

        public DialogueLine(string speaker, string text, string expression = null)
        {
            this.speaker = speaker;
            this.text = text;
            this.expression = expression;
        }
    }
}

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

    [Header("Morgana Expressions")]
    public GameObject morganaNeutral;
    public GameObject morganaSurprised;
    public GameObject morganaHappy;
    public GameObject morganaEmbarrassed;
    public GameObject morganaConfused;
    public GameObject morganaSad;
    public GameObject morganaExcited;

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

    // The line where Agnes turns around and walks off. Her sprite fades out as it finishes.
    const string AgnesExitLine = "[She flashed her signature mischievous smile to him, and turned around as she began walking.]";

    int introIndex = 0;
    bool isTyping = false; // Prevents spamming Next
    // Agnes does not appear on screen until her first line (case 30),
    // so character expressions stay hidden until the intro clears this flag
    bool expressionsHiddenUntilTunnelEntrance = true;

    // Which character the expression objects belong to right now
    string currentCharacter = "Morgana";

    // Last expression applied, so a character can be brought back after being hidden
    string lastExpression = "neutral";

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
        SetExpressionObject(charNeutral, false);
        SetExpressionObject(charSurprised, false);
        SetExpressionObject(charHappy, false);
        SetExpressionObject(charEmbarrassed, false);
        SetExpressionObject(charConfused, false);
        SetExpressionObject(charSad, false);
        SetExpressionObject(charExcited, false);

        SetExpressionObject(morganaNeutral, false);
        SetExpressionObject(morganaSurprised, false);
        SetExpressionObject(morganaHappy, false);
        SetExpressionObject(morganaEmbarrassed, false);
        SetExpressionObject(morganaConfused, false);
        SetExpressionObject(morganaSad, false);
        SetExpressionObject(morganaExcited, false);
    }

    void SetExpressionObject(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    void ShowExpression(string expression)
    {
        HideAllExpressions();

        if (expressionsHiddenUntilTunnelEntrance)
            return;

        bool morgana = currentCharacter == "Morgana";
        GameObject target = null;

        switch (expression)
        {
            case "neutral":     target = morgana ? morganaNeutral : charNeutral; break;
            case "surprised":   target = morgana ? morganaSurprised : charSurprised; break;
            case "happy":       target = morgana ? morganaHappy : charHappy; break;
            case "smile":       target = morgana ? morganaHappy : charHappy; break;
            case "embarrassed": target = morgana ? morganaEmbarrassed : charEmbarrassed; break;
            case "confused":    target = morgana ? morganaConfused : charConfused; break;
            case "sad":         target = morgana ? morganaSad : charSad; break;
            case "excited":     target = morgana ? morganaExcited : charExcited; break;
        }

        if (target != null)
            lastExpression = expression;

        SetExpressionObject(target, true);
    }

    /// <summary>
    /// Fades Agnes's own sprites and nobody else's, so Morgana stays on screen when Agnes walks
    /// off.
    /// </summary>
    void FadeOutAgnesExpression()
    {
        GameObject[] agnesExpressions =
        {
            charNeutral, charSurprised, charHappy, charEmbarrassed,
            charConfused, charSad, charExcited
        };

        foreach (GameObject expression in agnesExpressions)
        {
            if (expression != null && expression.activeSelf)
                StartCoroutine(FadeAndDisable(expression));
        }
    }

    void FadeOutActiveExpressions()
    {
        GameObject[] expressions =
        {
            charNeutral, charSurprised, charHappy, charEmbarrassed,
            charConfused, charSad, charExcited,
            morganaNeutral, morganaSurprised, morganaHappy, morganaEmbarrassed,
            morganaConfused, morganaSad, morganaExcited
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

        // Morgana is the one on screen from her introduction (case 15) until Agnes steps in (case 30)
        currentCharacter = index >= 30 ? "Agnes" : "Morgana";

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
                StartCoroutine(FadeAndDisable(Picture1));
                textToSpeak = "[...his eyes widened, something between confusion and astonishment washed over him.]";
                break;
            case 4:
                ShowExpression("embarrassed");
                textToSpeak = "THE SWEEPERS ARE FAR FROM RIGHT.";
                break;
            case 5:
                ShowExpression("happy");
                textToSpeak = "LET OUS OUTSIDE, THEY'RE HIDING IT FROM US.";
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
                expressionsHiddenUntilTunnelEntrance = false;
                StartCoroutine(FadeInObject(morganaHappy));
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
                // Agnes stays hidden until she actually steps in at case 30,
                // so the character sprite is not shown during Morgana's lines.
                ShowExpression("smile");
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
                // Agnes only appears from this line onwards
                ShowExpression("happy");
                StartCoroutine(FadeInObject(charHappy));
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
                // Picture4 is the base layer: fading Picture3 out here reveals it,
                // and Agnes is taken off screen while Picture4 is showing
                expressionsHiddenUntilTunnelEntrance = true;
                ShowExpression("embarrassed");
                StartCoroutine(FadeAndDisable(Picture3));
                textToSpeak = "You see this thing here?";
                break;
            case 34:
                ShowExpression("embarrassed");
                textToSpeak = "[Agnes turned herself sideways, and right off the bat...]";
                break;
            case 35:
                ShowExpression("embarrassed");
                textToSpeak = "[...Bay noticed the strange device she was wearing on her ear.]";
                break;

        }

        // No character name until Morgana introduces herself (case 15).
        // Morgana speaks from case 15, Agnes from case 30 onwards.
        string speaker = index < 15 || textToSpeak.StartsWith("[")
            ? ""
            : index >= 30 ? "Agnes" : "Morgana";

        charName.GetComponent<TMP_Text>().text = speaker;

        textBox.GetComponent<TMP_Text>().text = textToSpeak;
        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);

        // -------------------------------------------------
        // CHOICE POINT (after Case 35)
        // -------------------------------------------------

        if (index == 35)
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
        if (introIndex <= 35)
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
            new DialogueLine("", "[Agnes put her finger over the device. There was a small blinking blue light.]"),
            new DialogueLine("Agnes", "We call these Flocks."),
            new DialogueLine("Agnes", "They are wearable communication devices developed by the Research & Technology Committee and Engineering Committee..."),
            new DialogueLine("Agnes", "...aka, us!"),
            new DialogueLine("Agnes", "Aren't they cool? They can communicate up to thirty miles without interruption."),
            new DialogueLine("Agnes", "They use a special frequency called The Flock, meaning the DCP can't intercept us when communicating.")
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
            new DialogueLine("", "[Agnes put her finger over the device. There was a small blinking blue light.]"),
            new DialogueLine("Agnes", "Yup, you're right!"),
            new DialogueLine("Agnes", "We call these Flocks."),
            new DialogueLine("Agnes", "They are wearable communication devices developed by the Research & Technology Committee and Engineering Committee..."),
            new DialogueLine("Agnes", "...aka, us!"),
            new DialogueLine("Agnes", "Aren't they cool? They can communicate up to thirty miles without interruption."),
            new DialogueLine("Agnes", "They use a special frequency called The Flock, meaning the DCP can't intercept us when communicating.")
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
            new DialogueLine("Agnes", "Oh, they sure aren't!"),
            new DialogueLine("Agnes", "They adjust perfectly around your ear, no matter how big or small it is."),
            new DialogueLine("", "[Agnes put her finger over the device. There was a small blinking blue light.]"),
            new DialogueLine("Agnes", "We call these Flocks."),
            new DialogueLine("Agnes", "They are wearable communication devices developed by the Research & Technology Committee and Engineering Committee..."),
            new DialogueLine("Agnes", "...aka, us!"),
            new DialogueLine("Agnes", "Aren't they cool? They can communicate up to thirty miles without interruption."),
            new DialogueLine("Agnes", "They use a special frequency called The Flock, meaning the DCP can't intercept us when communicating.")
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
        new DialogueLine("", "[She turned back towards Bay to face him, Morgana watching the two.]"),
        new DialogueLine("Agnes", "Anyways, we have to get to work."),
        new DialogueLine("Agnes", "As you saw, Morgana is designing a poster."),
        new DialogueLine("Agnes", "We'll go print a bunch of those and stick them around the Dome."),
        new DialogueLine("", "[Morgana grinned, proud of herself for being mentioned.]"),
        new DialogueLine("Agnes", "Don't worry, you don't have to get yourself mixed with these numbskulls if you don't want to."),
        new DialogueLine("Agnes", "I'll eventually find something you can do. Meanwhile..."),
        new DialogueLine("Agnes", "...I know the perfect job you can do for me."),
        new DialogueLine("", AgnesExitLine),
        new DialogueLine("", "[What did she gave in store for him?]"),
        new DialogueLine("", "[Whatever it was, he had to be prepared.]")
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

        if (!string.IsNullOrEmpty(dialogueLine.speaker))
            currentCharacter = dialogueLine.speaker;

        // Picture3 comes back for the Flocks explanation, and Agnes with it
        if (dialogueLine.text == "We call these Flocks.")
        {
            expressionsHiddenUntilTunnelEntrance = false;
            StartCoroutine(FadeInObject(Picture3));
            ShowExpression(lastExpression);
        }

        if (!string.IsNullOrEmpty(dialogueLine.expression))
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

        // Agnes has turned and started walking away, so her expression leaves the frame with her
        // as this line ends. The fade runs on its own, so advancing early does not cut it short.
        if (textToSpeak == AgnesExitLine)
            FadeOutAgnesExpression();

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

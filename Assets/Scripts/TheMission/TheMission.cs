using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// NOTE: Unity needs a MonoBehaviour's file name to match its class name.
// If the inspector refuses it, rename this file to WestTubeV2.cs.
public class TheMission : MonoBehaviour
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
    public GameObject blackScreen;
    public GameObject textBox;
    [SerializeField] string textToSpeak;
    [SerializeField] int currentTextLength;
    [SerializeField] int textLength;
    [SerializeField] GameObject mainTextObject;
    [SerializeField] GameObject nextButton;
    [SerializeField] int eventPos = 0;
    [SerializeField] GameObject charName;
    [SerializeField] GameObject fadeOut;

    [Header("Audio")]
    [SerializeField] BackgroundMusicFader bgm;

    [Header("Scene Pictures")]
    public GameObject Picture1;
    public GameObject Picture2;
    public GameObject Picture3;

    // The line where Agnes turns around and walks off. Her sprite fades out as it finishes.
    const string AgnesExitLine = "[With that, Agnes turned and walked away, leaving Bay to contemplate the gravity of the situation]";

    // The last line inside the room. The room shots fade out as it finishes, leaving Bay out
    // in the narrow passage.
    const string RoomExitLine = "[Bay couldn't shake off that feeling of unease as he stepped out back into the narrow passage.]";

    // The closing lines dim the room and drain the music, so the scene ends dark and quiet
    const float BlackscreenFadeDuration = 6f;
    const float MusicSlowFadeDuration = 10f;

    int introIndex = 0;
    bool isTyping = false; // Prevents spamming Next

    // Morgana only walks into frame on her first line (case 3), so the expressions
    // stay hidden during the opening narration. They are hidden again while the poster
    // shots are up (case 9) and come back with Morgana at case 16.
    bool expressionsHiddenUntilMorganaAppears = true;

    // Which character the expression objects belong to right now
    string currentCharacter = "Morgana";

    // Last expression applied, so a character can be brought back after being hidden
    string lastExpression = "neutral";

    void Update()
    {
        textLength = TextCreator.charCount;
    }

    void Start()
    {
        // Falls back to the scene's music fader if it wasn't assigned in the inspector
        if (bgm == null)
            bgm = FindFirstObjectByType<BackgroundMusicFader>();

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
            charConfused, charSad, charExcited,
            morganaNeutral, morganaSurprised, morganaHappy, morganaEmbarrassed,
            morganaConfused, morganaSad, morganaExcited
        };
    }

    void HideAllExpressions()
    {
        expressionSet.HideAll();
    }

    void ShowExpression(string expression)
    {
        if (expressionsHiddenUntilMorganaAppears)
        {
            expressionSet.HideAll();
            return;
        }

        bool morgana = currentCharacter == "Morgana";
        GameObject target = null;

        switch (expression)
        {
            case "neutral":     target = morgana ? morganaNeutral : charNeutral; break;
            case "surprised":   target = morgana ? morganaSurprised : charSurprised; break;
            case "happy":       target = morgana ? morganaHappy : charHappy; break;
            case "embarrassed": target = morgana ? morganaEmbarrassed : charEmbarrassed; break;
            case "confused":    target = morgana ? morganaConfused : charConfused; break;
            case "sad":         target = morgana ? morganaSad : charSad; break;
            case "excited":     target = morgana ? morganaExcited : charExcited; break;
        }

        if (target != null)
            lastExpression = expression;

        // An unknown expression leaves nobody on screen, same as before
        if (target == null)
        {
            expressionSet.HideAll();
            return;
        }

        expressionSet.Show(target);
    }

    /// <summary>
    /// Fades Agnes's own sprites and nobody else's, so Morgana stays on screen when Agnes walks
    /// off.
    /// </summary>
    void FadeOutAgnesExpression()
    {
        expressionSet.Settle();

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

    /// <summary>
    /// Fades Morgana's own sprites out and leaves them disabled, for the poster close-ups.
    /// </summary>
    void FadeOutMorganaExpression()
    {
        expressionSet.Settle();

        GameObject[] morganaExpressions =
        {
            morganaNeutral, morganaSurprised, morganaHappy, morganaEmbarrassed,
            morganaConfused, morganaSad, morganaExcited
        };

        foreach (GameObject expression in morganaExpressions)
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

    // The poster reveal happens in two beats: the wider shot goes first, then the
    // monitor shot a few seconds later.
    IEnumerator FadeOutPosterPictures()
    {
        yield return StartCoroutine(FadeAndDisable(Picture1));
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(FadeAndDisable(Picture2));
    }

    /// <summary>
    /// Fades the three room shots out together so the passage behind them takes over.
    /// </summary>
    void FadeOutRoomPictures()
    {
        StartCoroutine(FadeAndDisable(Picture1));
        StartCoroutine(FadeAndDisable(Picture2));
        StartCoroutine(FadeAndDisable(Picture3));
    }

    /// <summary>
    /// Drains the scene's music away across the closing lines.
    /// </summary>
    void SlowFadeMusicOut()
    {
        if (bgm != null)
            bgm.FadeOutOver(MusicSlowFadeDuration);
    }

    IEnumerator FadeInObject(GameObject target, float duration = 1f)
    {
        if (target == null)
            yield break;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        target.SetActive(true);
        canvasGroup.alpha = 0f;

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

        isTyping = true;

        // Who the line belongs to. Narration is left empty.
        string speaker = "";

        switch (index)
        {
            case 0:
                // Opening shot only, nobody on screen yet
                ShowExpression("neutral");
                textToSpeak = "A few days later...";
                break;
            case 1:
                ShowExpression("neutral");
                textToSpeak = "[Bay became acquainted with the surroundings of the underground city, and how fast The Underdomers operate.]";
                break;
            case 2:
                ShowExpression("neutral");
                textToSpeak = "[Agnes had put him with the Research & Technology Committee, sitting next to Morgana.]";
                break;
            case 3:
                // Morgana's first line, so she walks into frame here
                speaker = "Morgana";
                expressionsHiddenUntilMorganaAppears = false;
                lastExpression = "happy";
                ShowExpression("happy");
                // The opening is held on black, so the room is revealed with Morgana
                StartCoroutine(FadeAndDisable(blackScreen));
                textToSpeak = "Hey, I didn't know ya could handle these machines.";
                break;
            case 4:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "Thought they were too complicated for ya.";
                break;
            case 5:
                ShowExpression("happy");
                textToSpeak = "[She said with genuine amusement, and Bay too, who was surprised at his own technical abilities.]";
                break;
            case 6:
                ShowExpression("neutral");
                textToSpeak = "[How did he know how to operate a computer? He didn't recall ever using one before.]";
                break;
            case 7:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "I mean, who hasn't touched a computer in their life, ain't I right?";
                break;
            case 8:
                speaker = "Morgana";
                ShowExpression("excited");
                textToSpeak = "Whatever, check this out!";
                break;
            case 9:
                // Morgana steps out of frame so the poster shots have the screen to themselves.
                // She walks back in at case 16.
                expressionsHiddenUntilMorganaAppears = true;
                FadeOutMorganaExpression();
                StartCoroutine(FadeOutPosterPictures());
                textToSpeak = "[She opened a poster, displaying on her monitor screen.]";
                break;
            case 10:
                textToSpeak = "[It was similar to the one he had read when he entered The Hidden Colony.]";
                break;
            case 11:
                textToSpeak = "[This one read:]";
                break;
            case 12:
                // Poster slogans, so no speaker name
                textToSpeak = "THEY ARE CONTAMINATING THE SURFACE.";
                break;
            case 13:
                textToSpeak = "THEY WANNA KEEP US DOWN HERE.";
                break;
            case 14:
                textToSpeak = "OPEN THE DOOR, PIGS!";
                break;
            case 15:
                textToSpeak = "DOWN WITH THE SWEEPERS.";
                break;
            case 16:
                // Camera and Morgana both come back for the plan
                speaker = "Morgana";
                expressionsHiddenUntilMorganaAppears = false;
                lastExpression = "neutral";
                ShowExpression("neutral");
                StartCoroutine(FadeInObject(Picture2));
                StartCoroutine(FadeInObject(Picture1));
                textToSpeak = "Like I said, we will spread these 'round the Dome. We have to be careful.";
                break;
            case 17:
                speaker = "Morgana";
                ShowExpression("sad");
                textToSpeak = "Those DCP officers have their eyes everywhere.";
                break;
            case 18:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "Lucky for us, they have a blind spot.";
                break;
            case 19:
                speaker = "Morgana";
                ShowExpression("excited");
                textToSpeak = "We will sneak out of here, all six of us from the Committee...";
                break;
            case 20:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "...including you, Bay.";
                break;
            case 21:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "Don't worry, they don't call me the Sneak Queen for nothing";
                break;
            case 22:
                ShowExpression("neutral");
                textToSpeak = "[Sneaking out and trying to stick posters everywhere, it sounded dangerous but...]";
                break;
            case 23:
                ShowExpression("happy");
                textToSpeak = "[...for whatever reason, he felt the thrill of it.]";
                break;
            case 24:
                ShowExpression("neutral");
                textToSpeak = "[He didn't feel as confused as he used to be, just a few days before.]";
                break;
            case 25:
                ShowExpression("neutral");
                textToSpeak = "[His mind was clearer, there was a purpose he must chase...]";
                break;
            case 26:
                ShowExpression("happy");
                textToSpeak = "[...just like them.]";
                break;
            case 27:
                speaker = "Morgana";
                ShowExpression("excited");
                textToSpeak = "I'll go pack up my stuff, the posters are all printed by now!";
                break;
            case 28:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "Oh, I almost forgot, you should put these on.";
                break;
            case 29:
                ShowExpression("happy");
                textToSpeak = "[Morgana pulled something out of her pocket, a pair of Flocks.]";
                break;
            case 30:
                ShowExpression("happy");
                textToSpeak = "[She handed the Flocks to Bay, who grabbed them and promptly put them on his ear.]";
                break;
            case 31:
                ShowExpression("happy");
                textToSpeak = "[Just like she had said, they adjusted perfectly around his ear so they wouldn't fall.]";
                break;
            case 32:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "See? Comfortable as ever.";
                break;
            case 33:
                // Picture3 fades out to the Flocks close-up, but Morgana still talks on this
                // line, so she stays on screen instead of leaving with the room.
                speaker = "Morgana";
                ShowExpression("neutral");
                StartCoroutine(FadeAndDisable(Picture3));
                textToSpeak = "Now, let's go. That's all you need for now.";
                break;
            case 34:
                // Back to the room. Morgana never left, so only the picture fades back in.
                speaker = "Morgana";
                ShowExpression("neutral");
                StartCoroutine(FadeInObject(Picture3));
                textToSpeak = "We'll be keeping in touch with whoever's left in here.";
                break;
            case 35:
                speaker = "Morgana";
                ShowExpression("sad");
                textToSpeak = "They'll warn us if something's wrong, those Dome Civil Pigs are everywhere...";
                break;
            case 36:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "...you really stirred up the hive, didn't ya?";
                break;
            case 37:
                ShowExpression("happy");
                textToSpeak = "[Morgana flashed him a grin, then walked to a room he presumed was a storage room.]";
                break;
            case 38:
                // Morgana leaves the room and Agnes walks in
                currentCharacter = "Agnes";
                lastExpression = "neutral";
                ShowExpression("neutral");
                textToSpeak = "[As Morgana entered the door, Bay saw Agnes approaching him with a determined look on her face.]";
                break;
            case 39:
                speaker = "Agnes";
                ShowExpression("sad");
                textToSpeak = "You be safe out there, Bay. Don't worry, we will be monitoring.";
                break;
            case 40:
                speaker = "Agnes";
                ShowExpression("happy");
                textToSpeak = "Those airheads may outnumber us, but they don't got the brain power like us.";
                break;
            case 41:
                speaker = "Agnes";
                ShowExpression("neutral");
                textToSpeak = "Come on, I'll be staying here. You go with them.";
                break;
            case 42:
                speaker = "Agnes";
                ShowExpression("sad");
                textToSpeak = "I know this is all really sudden for you...";
                break;
            case 43:
                speaker = "Agnes";
                ShowExpression("sad");
                textToSpeak = "...this is for a greater cause, trust me.";
                break;
            case 44:
                speaker = "Agnes";
                ShowExpression("sad");
                textToSpeak = "...";
                break;
            case 45:
                speaker = "Agnes";
                ShowExpression("happy");
                textToSpeak = "Best of luck, Bay.";
                break;
            case 46:
                ShowExpression("sad");
                textToSpeak = AgnesExitLine;
                break;
            case 47:
                // Morgana comes back for the way out
                speaker = "";
                currentCharacter = "Morgana";
                ShowExpression("happy");
                textToSpeak = "[Morgana returned, and she let him out alongside the other members of the Committee.]";
                break;
            case 48:
                ShowExpression("neutral");
                textToSpeak = "[Bay couldn't shake off that feeling of unease as he stepped out back into the narrow passage.]";
                break;
            case 49:
                ShowExpression("neutral");
                textToSpeak = "[He knew it was risky, that they could get caught. What would happen then, he didn't know.]";
                break;
            case 50:
                ShowExpression("happy");
                textToSpeak = "[Morgana gave him a reassuring smile, easing some of his anxiety. She began speaking:]";
                break;
            case 51:
                speaker = "Morgana";
                ShowExpression("happy");
                textToSpeak = "Come on, Bay. We have a lot to do.";
                break;
            case 52:
                speaker = "Morgana";
                ShowExpression("neutral");
                // The scene starts winding down here: the room slowly goes dark and the
                // music drains away under the closing lines.
                StartCoroutine(FadeInObject(blackScreen, BlackscreenFadeDuration));
                SlowFadeMusicOut();
                textToSpeak = "Let this be the catalyst for change...";
                break;
            case 53:
                speaker = "Morgana";
                ShowExpression("neutral");
                textToSpeak = "...let us begin...";
                break;
            case 54:
                speaker = "Morgana";
                ShowExpression("excited");
                textToSpeak = "...Operation Newspread.";
                break;
        }

        charName.GetComponent<TMP_Text>().text = speaker;

        textBox.GetComponent<TMP_Text>().text = textToSpeak;
        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);

        // Agnes has turned and started walking away, so her expression leaves the frame with her
        // as this line ends. The fade runs on its own, so advancing early does not cut it short.
        if (textToSpeak == AgnesExitLine)
            FadeOutAgnesExpression();

        // Bay steps out of the room as this line ends, so the room shots leave with him
        if (textToSpeak == RoomExitLine)
            FadeOutRoomPictures();

        nextButton.SetActive(true);
        isTyping = false;
    }

    // NextButton to be called by Unity UI OnClick
    public void NextButton()
    {
        if (isTyping) return; // Don't skip the typing animation

        introIndex++;

        // The intro has 55 lines (0-54)
        if (introIndex <= 54)
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
        // TheMission is the last scene of Chapter 1, so it hands over to the Credits.
        yield return SceneManager.LoadSceneAsync(2);
    }
}

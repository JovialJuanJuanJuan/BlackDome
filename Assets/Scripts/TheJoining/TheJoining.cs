using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TheJoining : MonoBehaviour
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

    [Header("Audio")]
    [SerializeField] BackgroundMusicFader bgmFader;

    public GameObject Picture1;
    public GameObject Picture2;

    // How fast the music is cut on the reveal line, and how fast it swells back
    const float MusicCutDuration = 0.25f;
    const float MusicResumeDuration = 0.5f;

    // Slow drain across the closing lines, so the scene ends in near-silence
    const float MusicSlowFadeDuration = 10f;

    // Extra pause before the Next button on the music cut line
    const float SuspenseNextDelay = 1.2f;

    int introIndex = 0;
    bool isTyping = false; // Prevents spamming Next

    // The last intro line ("...for a greater cause.")
    const int LastIntroIndex = 26;

    // Turns off the expression blocking once Agnes speaks (her line in case 12)
    bool expressionsHiddenUntilAgnesSpeaks = true;

    void Update()
    {
        textLength = TextCreator.charCount;
    }

    void Start()
    {
        // Agnes stays expressionless through the narrated intro;
        // her expressions come back on her first spoken line
        expressionsHiddenUntilAgnesSpeaks = true;
        HideAllExpressions();

        // Falls back to the scene's music fader if it wasn't assigned in the inspector
        if (bgmFader == null)
            bgmFader = FindFirstObjectByType<BackgroundMusicFader>();

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

    void ShowExpression(string expression)
    {
        HideAllExpressions();

        if (expressionsHiddenUntilAgnesSpeaks)
            return;

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

        if (target != null)
            StartCoroutine(FadeInExpression(target));
    }

    // Expressions with their own Animator animate themselves in, so they are just
    // switched on. The rest ease in: a soft scale-up from slightly smaller with a
    // fade, rather than popping into place.
    IEnumerator FadeInExpression(GameObject target)
    {
        if (target == null)
            yield break;

        if (target.GetComponent<Animator>() != null)
        {
            target.SetActive(true);
            yield break;
        }

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        Vector3 finalScale = target.transform.localScale;
        Vector3 startScale = finalScale * 0.92f;

        target.SetActive(true);
        canvasGroup.alpha = 0f;
        target.transform.localScale = startScale;

        const float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Ease-out so the sprite settles into place instead of stopping dead
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            canvasGroup.alpha = eased;
            target.transform.localScale = Vector3.LerpUnclamped(startScale, finalScale, eased);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        target.transform.localScale = finalScale;
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
                textToSpeak = "[Bay had followed Agnes through a narrow passage...]";
                break;
            case 1:
                textToSpeak = "[...similar to the one they had just travelled through not too long ago.]";
                break;
            case 2:
                textToSpeak = "[There were papers taped to the walls, rushed handwriting filling the sheets.]";
                break;
            case 3:
                textToSpeak = "[He was confused, a lot had happened in so little time.]";
                break;
            case 4:
                textToSpeak = "[But, he imagined The Underdomers must move as quick everyday to avoid getting caught.]";
                break;
            case 5:
                textToSpeak = "[At the end of the passage, there were a handful of small black lockers.]";
                break;
            case 6:
                textToSpeak = "[Agnes pulled a key from her pocket and inserted it into one of the lockers.]";
                break;
            case 7:
                textToSpeak = "[Once open, she pulled a paper out and handed it directly to Bay.]";
                break;
            case 8:
                textToSpeak = "[He felt as though this paper held the entire weight of the world on it.]";
                break;
            case 9:
                textToSpeak = "[A confidential message? Only for him to read?]";
                break;
            case 10:
                textToSpeak = "[Agnes smiled, noticing his confusion and concern.]";
                break;
            case 11:
                textToSpeak = "[She began speaking through the heavy atmosphere:]";
                break;
            case 12:
                // Agnes speaks for the first time here, so her expressions return
                expressionsHiddenUntilAgnesSpeaks = false;
                ShowExpression("smile");
                textToSpeak = "Don't worry, silly. It's not anything serious.";
                break;
            case 13:
                textToSpeak = "This is the most important part of them all...";
                break;
            case 14:
                textToSpeak = "...and the most exciting part, too.";
                break;
            case 15:
                textToSpeak = "It's time for you...";
                break;
            case 16:
                // The music is cut right on the reveal, leaving the line in silence
                QuickCutMusic();
                textToSpeak = "...to join us, The Underdomers!";
                break;
            case 17:
                // ...and comes back on the same beat it left, exactly where it paused
                ResumeMusic();
                textToSpeak = "And, in order to join us, you must sign your name in this paper!";
                break;
            case 18:
                textToSpeak = "Go ahead, I'll wait.";
                break;
            case 19:
                textToSpeak = "[She gave him a quick smile, putting him at ease.]";
                break;
            case 20:
                textToSpeak = "[Joining the Underdomers. A group full of teenagers like him.]";
                break;
            case 21:
                textToSpeak = "[It felt thrilling, yet a little intimidating.]";
                break;
            case 22:
                textToSpeak = "[He didn't have time to think about it, Agnes handed him a clipboard and a pen to write.]";
                break;
            case 23:
                textToSpeak = "[He took the clipboard and pen, feeling a mix of excitement and nervousness.]";
                break;
            case 24:
                // The music starts draining here and is gone by the final line
                SlowFadeMusicOut();
                textToSpeak = "[He held it close to hist chest, he was about to commit...]";
                break;
            case 25:
                textToSpeak = "[...commit to the Underdomers...]";
                break;
            case 26:
                textToSpeak = "[...for a greater cause.]";
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

        // On the lockers line Picture1 fades out. Picture2 is left untouched.
        if (index == 5)
        {
            yield return StartCoroutine(FadePicture(Picture1, false));
        }

        // Let the silence sit for a moment before the player can move on
        if (index == 16)
        {
            yield return new WaitForSeconds(SuspenseNextDelay);
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

    void QuickCutMusic()
    {
        if (bgmFader != null)
            bgmFader.QuickFadeOutAndPause(MusicCutDuration);
    }

    void ResumeMusic()
    {
        if (bgmFader != null)
            bgmFader.ResumeAfterQuickFade(MusicResumeDuration);
    }

    void SlowFadeMusicOut()
    {
        if (bgmFader != null)
            bgmFader.FadeOutOver(MusicSlowFadeDuration);
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
        // The scene ends on the paper Bay is asked to sign by Agnes for him to join.
        yield return SceneManager.LoadSceneAsync(15);
    }
}
